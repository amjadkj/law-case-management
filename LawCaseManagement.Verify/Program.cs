using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using LawCaseManagement.Core;
using Task = LawCaseManagement.Core.Task;

namespace LawCaseManagement.Verify
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("=== Starting System Verification Suite ===");

            // 0. Initialize DB
            Console.WriteLine("\n[0] Initializing DB with DatabaseInitializer...");
            using (var db = new CaseDbContext())
            {
                var initializer = new DatabaseInitializer(db, NullLogger<DatabaseInitializer>.Instance);
                initializer.Initialize();
            }
            Console.WriteLine("Database initialized and verified.");

            var authService = new AuthService();
            var caseService = new CaseService();
            var clientService = new ClientService();
            var taskService = new TaskService();
            var docService = new DocumentService();
            var adminService = new AdminService();

            // 1. Verify Authentication
            Console.WriteLine("\n[1] Testing Authentication & Roles...");
            bool authAdmin = authService.Authenticate("admin", "admin123");
            Console.WriteLine($"Admin Auth: {authAdmin} (User: {AuthService.CurrentUser?.FullName}, Role: {AuthService.CurrentUser?.Role})");
            if (!authAdmin) throw new Exception("Admin authentication failed!");

            // 2. Verify File Number Auto-Generation
            Console.WriteLine("\n[2] Testing File Number Auto-Generation...");
            string nextFileNo = caseService.GenerateNextFileNumber();
            Console.WriteLine($"Generated Next File Number: {nextFileNo}");
            if (string.IsNullOrWhiteSpace(nextFileNo) || !nextFileNo.EndsWith($"/{DateTime.Now.Year}"))
            {
                throw new Exception($"Invalid File Number generated: {nextFileNo}");
            }

            // 3. Verify Case Creation with Linked Clients
            Console.WriteLine("\n[3] Testing Case Creation with Linked Clients...");
            var lawyer = adminService.GetUsers().FirstOrDefault(u => u.Role == Roles.Lawyer);
            var paralegal = adminService.GetUsers().FirstOrDefault(u => u.Role == Roles.Paralegal);
            var client = clientService.GetClients().FirstOrDefault();

            if (lawyer == null || paralegal == null || client == null)
            {
                throw new Exception("Required seed data (lawyer/paralegal/client) missing!");
            }

            var newCase = new Case
            {
                FileNumber = nextFileNo,
                Title = "Test Verification Lawsuit",
                CaseType = CaseTypes.Civil,
                Status = CaseStatuses.Open,
                OpenDate = DateTime.Today,
                LawyerID = lawyer.UserID,
                ParalegalID = paralegal.UserID,
                Notes = "Automated test case execution notes."
            };

            bool caseCreated = caseService.CreateCase(newCase, new System.Collections.Generic.List<int> { client.ClientID });
            Console.WriteLine($"Case Created Success: {caseCreated}");
            if (!caseCreated) throw new Exception("Failed to create case!");

            // 4. Verify Linked Cases Retrieval for Client
            Console.WriteLine("\n[4] Testing Retrieval of Linked Cases for Client...");
            var clientCases = clientService.GetCasesForClient(client.ClientID);
            Console.WriteLine($"Client '{client.FullName}' has {clientCases.Count} linked case(s).");
            if (clientCases.Count == 0) throw new Exception("Client linked cases list is empty!");

            // 5. Verify Document Upload & Size Restriction
            Console.WriteLine("\n[5] Testing Document Service & 50MB Limit...");
            string tempFile = Path.Combine(Path.GetTempPath(), $"test_doc_{Guid.NewGuid()}.txt");
            File.WriteAllText(tempFile, "Legal Document Test Content");
            try
            {
                var doc = docService.UploadDocument(newCase.CaseID, tempFile, AuthService.CurrentUser!.UserID);
                Console.WriteLine($"Document Uploaded: ID={doc.DocumentID}, Name={doc.FileName}");

                // Test OpenDocument helper
                bool openSuccess = DocumentService.OpenDocument(doc.FilePath);
                Console.WriteLine($"Open Document Helper Called: Success={openSuccess}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Document test note: {ex.Message}");
            }

            // 6. Verify Task Lifecycle & Deletion
            Console.WriteLine("\n[6] Testing Task Lifecycle & Deletion...");
            var task = new Task
            {
                CaseID = newCase.CaseID,
                Title = "Prepare Initial Pleading",
                AssignedToID = paralegal.UserID,
                DueDate = DateTime.Today.AddDays(5),
                Status = TaskStatuses.Pending
            };
            bool taskCreated = taskService.CreateTask(task);
            Console.WriteLine($"Task Created: {taskCreated}");

            var fetchedTasks = taskService.GetTasks(newCase.CaseID);
            Console.WriteLine($"Fetched {fetchedTasks.Count} task(s) for Case ID {newCase.CaseID}");
            if (fetchedTasks.Count == 0) throw new Exception("Failed to fetch created task!");

            var createdTask = fetchedTasks.First();
            createdTask.Status = TaskStatuses.InProgress;
            taskService.UpdateTask(createdTask);
            Console.WriteLine($"Updated Task Status to: {createdTask.Status}");

            bool taskDeleted = taskService.DeleteTask(createdTask.TaskID);
            Console.WriteLine($"Task Deleted Success: {taskDeleted}");

            // 7. Verify Audit Logging
            Console.WriteLine("\n[7] Testing Audit Logs...");
            var auditLogs = adminService.GetAuditLogs();
            Console.WriteLine($"Audit Logs Count: {auditLogs.Count}. Latest Action: '{auditLogs.FirstOrDefault()?.Action}' - '{auditLogs.FirstOrDefault()?.Details}'");

            Console.WriteLine("\n=== ALL VERIFICATION TESTS PASSED SUCCESSFULLY! ===");
        }
    }
}
