using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LawCaseManagement.Core
{
    // ─── Audit Service ────────────────────────────────────────────────────────
    public interface IAuditService
    {
        void Log(int? userId, string action, string details);
    }

    public class AuditService : IAuditService
    {
        private readonly IDbContextFactory<CaseDbContext>? _contextFactory;
        private readonly ILogger<AuditService> _logger;

        public AuditService(IDbContextFactory<CaseDbContext>? contextFactory = null, ILogger<AuditService>? logger = null)
        {
            _contextFactory = contextFactory;
            _logger = logger ?? NullLogger<AuditService>.Instance;
        }

        public void Log(int? userId, string action, string details)
        {
            try
            {
                using var db = CreateDbContext();
                db.AuditLogs.Add(new AuditLog
                {
                    UserID = userId,
                    Action = action,
                    Details = details,
                    Timestamp = DateTime.UtcNow
                });
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing audit log: {Action}", action);
            }
        }

        public static void LogAction(int? userId, string action, string details)
        {
            try
            {
                using var db = new CaseDbContext();
                db.AuditLogs.Add(new AuditLog
                {
                    UserID = userId,
                    Action = action,
                    Details = details,
                    Timestamp = DateTime.UtcNow
                });
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging audit: {ex.Message}");
            }
        }

        private CaseDbContext CreateDbContext() => _contextFactory != null ? _contextFactory.CreateDbContext() : new CaseDbContext();
    }

    // ─── Authentication Service ───────────────────────────────────────────────
    public interface IAuthService
    {
        User? CurrentUser { get; }
        bool Authenticate(string username, string password);
        void Logout();
    }

    public class AuthService : IAuthService
    {
        private readonly IDbContextFactory<CaseDbContext>? _contextFactory;
        private readonly IAuditService _auditService;
        private readonly ILogger<AuthService> _logger;

        public static User? CurrentUser { get; set; }
        User? IAuthService.CurrentUser => CurrentUser;

        public AuthService(
            IDbContextFactory<CaseDbContext>? contextFactory = null,
            IAuditService? auditService = null,
            ILogger<AuthService>? logger = null)
        {
            _contextFactory = contextFactory;
            _auditService = auditService ?? new AuditService(contextFactory);
            _logger = logger ?? NullLogger<AuthService>.Instance;
        }

        public bool Authenticate(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return false;

            try
            {
                using var db = CreateDbContext();
                var user = db.Users.FirstOrDefault(u => u.Username.ToLower() == username.ToLower());
                if (user == null || !user.IsActive)
                    return false;

                bool isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                if (isValid)
                {
                    CurrentUser = user;
                    _auditService.Log(user.UserID, "User Login", $"User {user.Username} successfully logged in.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication for user {Username}", username);
            }

            return false;
        }

        public void Logout()
        {
            if (CurrentUser != null)
            {
                _auditService.Log(CurrentUser.UserID, "User Logout", $"User {CurrentUser.Username} logged out.");
                CurrentUser = null;
            }
        }

        private CaseDbContext CreateDbContext() => _contextFactory != null ? _contextFactory.CreateDbContext() : new CaseDbContext();
    }

    // ─── Case Management Service ─────────────────────────────────────────────
    public interface ICaseService
    {
        List<Case> GetCases(string? searchQuery = null, string? statusFilter = null, string? typeFilter = null, int? assignedUserFilter = null);
        Case? GetCaseById(int caseId);
        bool CreateCase(Case c, List<int> clientIds);
        bool UpdateCase(Case updatedCase, List<int> clientIds);
        string GenerateNextFileNumber();
    }

    public class CaseService : ICaseService
    {
        private readonly IDbContextFactory<CaseDbContext>? _contextFactory;
        private readonly IAuditService _auditService;
        private readonly ILogger<CaseService> _logger;

        public CaseService(
            IDbContextFactory<CaseDbContext>? contextFactory = null,
            IAuditService? auditService = null,
            ILogger<CaseService>? logger = null)
        {
            _contextFactory = contextFactory;
            _auditService = auditService ?? new AuditService(contextFactory);
            _logger = logger ?? NullLogger<CaseService>.Instance;
        }

        private CaseDbContext CreateDbContext() => _contextFactory != null ? _contextFactory.CreateDbContext() : new CaseDbContext();

        public List<Case> GetCases(string? searchQuery = null, string? statusFilter = null, string? typeFilter = null, int? assignedUserFilter = null)
        {
            using var db = CreateDbContext();
            var query = db.Cases
                .Include(c => c.Lawyer)
                .Include(c => c.Paralegal)
                .Include(c => c.CaseClients)
                    .ThenInclude(cc => cc.Client)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                searchQuery = searchQuery.ToLower();
                query = query.Where(c => c.FileNumber.ToLower().Contains(searchQuery) ||
                                         c.Title.ToLower().Contains(searchQuery) ||
                                         c.Notes.ToLower().Contains(searchQuery) ||
                                         c.CaseClients.Any(cc => cc.Client.FullName.ToLower().Contains(searchQuery)));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
            {
                query = query.Where(c => c.Status == statusFilter);
            }

            if (!string.IsNullOrWhiteSpace(typeFilter) && typeFilter != "All")
            {
                query = query.Where(c => c.CaseType == typeFilter);
            }

            if (assignedUserFilter.HasValue)
            {
                query = query.Where(c => c.LawyerID == assignedUserFilter.Value || c.ParalegalID == assignedUserFilter.Value);
            }

            return query.OrderByDescending(c => c.CreatedAt).ToList();
        }

        public Case? GetCaseById(int caseId)
        {
            using var db = CreateDbContext();
            return db.Cases
                .Include(c => c.Lawyer)
                .Include(c => c.Paralegal)
                .Include(c => c.CaseClients)
                    .ThenInclude(cc => cc.Client)
                .Include(c => c.Tasks)
                    .ThenInclude(t => t.AssignedTo)
                .Include(c => c.Documents)
                    .ThenInclude(d => d.UploadedBy)
                .FirstOrDefault(c => c.CaseID == caseId);
        }

        public bool CreateCase(Case c, List<int> clientIds)
        {
            using var db = CreateDbContext();
            using var transaction = db.Database.BeginTransaction();
            try
            {
                // Enforce server-side role validation
                if (AuthService.CurrentUser != null &&
                    AuthService.CurrentUser.Role != Roles.Admin &&
                    AuthService.CurrentUser.Role != Roles.Lawyer)
                {
                    throw new UnauthorizedAccessException("Only Lawyers and Administrators may create cases.");
                }

                db.Cases.Add(c);
                db.SaveChanges();

                if (clientIds != null && clientIds.Count > 0)
                {
                    foreach (int clientId in clientIds)
                    {
                        db.CaseClients.Add(new CaseClient { CaseID = c.CaseID, ClientID = clientId });
                    }
                    db.SaveChanges();
                }

                transaction.Commit();
                _auditService.Log(AuthService.CurrentUser?.UserID, "Create Case", $"Created case FileNo: {c.FileNumber}, Title: {c.Title}");
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Error creating case with FileNumber {FileNumber}", c.FileNumber);
                return false;
            }
        }

        public bool UpdateCase(Case updatedCase, List<int> clientIds)
        {
            using var db = CreateDbContext();
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var existingCase = db.Cases.FirstOrDefault(c => c.CaseID == updatedCase.CaseID);
                if (existingCase == null) return false;

                existingCase.FileNumber = updatedCase.FileNumber;
                existingCase.Title = updatedCase.Title;
                existingCase.CaseType = updatedCase.CaseType;
                existingCase.Status = updatedCase.Status;
                existingCase.LawyerID = updatedCase.LawyerID;
                existingCase.ParalegalID = updatedCase.ParalegalID;
                existingCase.Notes = updatedCase.Notes;

                if (updatedCase.Status == CaseStatuses.Closed && !existingCase.CloseDate.HasValue)
                {
                    existingCase.CloseDate = DateTime.Today;
                }
                else if (updatedCase.Status != CaseStatuses.Closed)
                {
                    existingCase.CloseDate = null;
                }

                var existingLinks = db.CaseClients.Where(cc => cc.CaseID == updatedCase.CaseID).ToList();
                db.CaseClients.RemoveRange(existingLinks);

                if (clientIds != null)
                {
                    foreach (int clientId in clientIds)
                    {
                        db.CaseClients.Add(new CaseClient { CaseID = updatedCase.CaseID, ClientID = clientId });
                    }
                }

                db.SaveChanges();
                transaction.Commit();
                _auditService.Log(AuthService.CurrentUser?.UserID, "Update Case", $"Updated case FileNo: {updatedCase.FileNumber}, Title: {updatedCase.Title}");
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Error updating case ID {CaseID}", updatedCase.CaseID);
                return false;
            }
        }

        public string GenerateNextFileNumber()
        {
            int currentYear = DateTime.Now.Year;
            string yearSuffix = $"/{currentYear}";

            using var db = CreateDbContext();
            var fileNumbers = db.Cases
                .Where(c => c.FileNumber != null && c.FileNumber.EndsWith(yearSuffix))
                .Select(c => c.FileNumber)
                .ToList();

            int maxNumber = 0;
            foreach (var fn in fileNumbers)
            {
                var parts = fn.Split('/');
                if (parts.Length == 2 && int.TryParse(parts[0], out int num))
                {
                    if (num > maxNumber)
                        maxNumber = num;
                }
            }

            return $"{maxNumber + 1}/{currentYear}";
        }
    }

    // ─── Client Management Service ───────────────────────────────────────────
    public interface IClientService
    {
        List<Client> GetClients(string? searchQuery = null);
        List<Case> GetCasesForClient(int clientId);
        bool CreateClient(Client c);
        bool UpdateClient(Client c);
    }

    public class ClientService : IClientService
    {
        private readonly IDbContextFactory<CaseDbContext>? _contextFactory;
        private readonly IAuditService _auditService;
        private readonly ILogger<ClientService> _logger;

        public ClientService(
            IDbContextFactory<CaseDbContext>? contextFactory = null,
            IAuditService? auditService = null,
            ILogger<ClientService>? logger = null)
        {
            _contextFactory = contextFactory;
            _auditService = auditService ?? new AuditService(contextFactory);
            _logger = logger ?? NullLogger<ClientService>.Instance;
        }

        private CaseDbContext CreateDbContext() => _contextFactory != null ? _contextFactory.CreateDbContext() : new CaseDbContext();

        public List<Client> GetClients(string? searchQuery = null)
        {
            using var db = CreateDbContext();
            var query = db.Clients.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                searchQuery = searchQuery.ToLower();
                query = query.Where(c => c.FullName.ToLower().Contains(searchQuery) ||
                                         c.Phone.Contains(searchQuery) ||
                                         c.Address.ToLower().Contains(searchQuery) ||
                                         c.Email.ToLower().Contains(searchQuery));
            }

            return query.OrderBy(c => c.FullName).ToList();
        }

        public List<Case> GetCasesForClient(int clientId)
        {
            using var db = CreateDbContext();
            return db.CaseClients
                .Where(cc => cc.ClientID == clientId)
                .Include(cc => cc.Case)
                    .ThenInclude(c => c.Lawyer)
                .Include(cc => cc.Case)
                    .ThenInclude(c => c.Paralegal)
                .Select(cc => cc.Case)
                .OrderByDescending(c => c.CreatedAt)
                .ToList();
        }

        public bool CreateClient(Client c)
        {
            try
            {
                using var db = CreateDbContext();
                db.Clients.Add(c);
                db.SaveChanges();

                _auditService.Log(AuthService.CurrentUser?.UserID, "Create Client", $"Created client profile for {c.FullName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating client {FullName}", c.FullName);
                return false;
            }
        }

        public bool UpdateClient(Client c)
        {
            try
            {
                using var db = CreateDbContext();
                var existingClient = db.Clients.FirstOrDefault(cl => cl.ClientID == c.ClientID);
                if (existingClient == null) return false;

                existingClient.FullName = c.FullName;
                existingClient.Address = c.Address;
                existingClient.Phone = c.Phone;
                existingClient.Email = c.Email;

                db.SaveChanges();
                _auditService.Log(AuthService.CurrentUser?.UserID, "Update Client", $"Updated client profile for {c.FullName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating client ID {ClientID}", c.ClientID);
                return false;
            }
        }
    }

    // ─── Task Management Service ─────────────────────────────────────────────
    public interface ITaskService
    {
        List<Task> GetTasks(int? caseId = null, int? assignedToId = null, string? statusFilter = null);
        bool CreateTask(Task t);
        bool UpdateTask(Task t);
        bool DeleteTask(int taskId);
    }

    public class TaskService : ITaskService
    {
        private readonly IDbContextFactory<CaseDbContext>? _contextFactory;
        private readonly IAuditService _auditService;
        private readonly ILogger<TaskService> _logger;

        public TaskService(
            IDbContextFactory<CaseDbContext>? contextFactory = null,
            IAuditService? auditService = null,
            ILogger<TaskService>? logger = null)
        {
            _contextFactory = contextFactory;
            _auditService = auditService ?? new AuditService(contextFactory);
            _logger = logger ?? NullLogger<TaskService>.Instance;
        }

        private CaseDbContext CreateDbContext() => _contextFactory != null ? _contextFactory.CreateDbContext() : new CaseDbContext();

        public List<Task> GetTasks(int? caseId = null, int? assignedToId = null, string? statusFilter = null)
        {
            using var db = CreateDbContext();
            var query = db.Tasks
                .Include(t => t.Case)
                .Include(t => t.AssignedTo)
                .AsQueryable();

            if (caseId.HasValue)
            {
                query = query.Where(t => t.CaseID == caseId.Value);
            }

            if (assignedToId.HasValue)
            {
                query = query.Where(t => t.AssignedToID == assignedToId.Value);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
            {
                query = query.Where(t => t.Status == statusFilter);
            }

            return query.OrderBy(t => t.DueDate).ToList();
        }

        public bool CreateTask(Task t)
        {
            try
            {
                using var db = CreateDbContext();
                db.Tasks.Add(t);
                db.SaveChanges();

                _auditService.Log(AuthService.CurrentUser?.UserID, "Create Task", $"Created task \"{t.Title}\" for case ID {t.CaseID}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating task \"{Title}\"", t.Title);
                return false;
            }
        }

        public bool UpdateTask(Task t)
        {
            try
            {
                using var db = CreateDbContext();
                var existingTask = db.Tasks.FirstOrDefault(tsk => tsk.TaskID == t.TaskID);
                if (existingTask == null) return false;

                existingTask.Title = t.Title;
                existingTask.Status = t.Status;
                existingTask.DueDate = t.DueDate;
                existingTask.AssignedToID = t.AssignedToID;

                db.SaveChanges();
                _auditService.Log(AuthService.CurrentUser?.UserID, "Update Task", $"Updated task \"{t.Title}\", Status: {t.Status}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating task ID {TaskID}", t.TaskID);
                return false;
            }
        }

        public bool DeleteTask(int taskId)
        {
            try
            {
                using var db = CreateDbContext();
                var task = db.Tasks.FirstOrDefault(t => t.TaskID == taskId);
                if (task == null) return false;

                db.Tasks.Remove(task);
                db.SaveChanges();

                _auditService.Log(AuthService.CurrentUser?.UserID, "Delete Task", $"Deleted task \"{task.Title}\" (Task ID {taskId})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting task ID {TaskId}", taskId);
                return false;
            }
        }
    }

    // ─── Document Management Service ─────────────────────────────────────────
    public interface IDocumentService
    {
        string StorageDirectory { get; }
        Document UploadDocument(int caseId, string sourceFilePath, int userId);
        bool DeleteDocument(int documentId, int userId);
    }

    public class DocumentService : IDocumentService
    {
        private readonly IDbContextFactory<CaseDbContext>? _contextFactory;
        private readonly IAuditService _auditService;
        private readonly ILogger<DocumentService> _logger;
        private readonly string _storageDirectory;

        public static string DocumentsStorageDirectory { get; set; } = GetDefaultStorageDirectory();
        public string StorageDirectory => _storageDirectory;

        public DocumentService(
            IDbContextFactory<CaseDbContext>? contextFactory = null,
            AppConfig? config = null,
            IAuditService? auditService = null,
            ILogger<DocumentService>? logger = null)
        {
            _contextFactory = contextFactory;
            _auditService = auditService ?? new AuditService(contextFactory);
            _logger = logger ?? NullLogger<DocumentService>.Instance;

            if (!string.IsNullOrWhiteSpace(config?.Documents?.SharedFolder))
            {
                _storageDirectory = config.Documents.SharedFolder;
            }
            else
            {
                _storageDirectory = DocumentsStorageDirectory;
            }
        }

        private static string GetDefaultStorageDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "LawCaseManagement", "SharedDocuments");
        }

        private CaseDbContext CreateDbContext() => _contextFactory != null ? _contextFactory.CreateDbContext() : new CaseDbContext();

        public Document UploadDocument(int caseId, string sourceFilePath, int userId)
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Source file not found.", sourceFilePath);

            long fileLength = new FileInfo(sourceFilePath).Length;
            if (fileLength > 50 * 1024 * 1024)
            {
                throw new InvalidOperationException("File size exceeds the maximum limit of 50 MB.");
            }

            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }

            string originalFileName = Path.GetFileName(sourceFilePath);
            string uniqueFileName = $"{Guid.NewGuid()}_{originalFileName}";
            string destinationPath = Path.Combine(_storageDirectory, uniqueFileName);

            File.Copy(sourceFilePath, destinationPath, true);

            var doc = new Document
            {
                CaseID = caseId,
                FileName = originalFileName,
                FilePath = destinationPath,
                UploadedByID = userId,
                UploadDate = DateTime.UtcNow
            };

            using (var db = CreateDbContext())
            {
                db.Documents.Add(doc);
                db.SaveChanges();
            }

            _auditService.Log(userId, "Upload Document", $"Uploaded document \"{originalFileName}\" for case ID {caseId}");
            return doc;
        }

        public bool DeleteDocument(int documentId, int userId)
        {
            try
            {
                using var db = CreateDbContext();
                var doc = db.Documents.FirstOrDefault(d => d.DocumentID == documentId);
                if (doc == null) return false;

                if (File.Exists(doc.FilePath))
                {
                    try { File.Delete(doc.FilePath); } catch { /* best effort */ }
                }

                db.Documents.Remove(doc);
                db.SaveChanges();

                _auditService.Log(userId, "Delete Document", $"Deleted document link and file for \"{doc.FileName}\" (Case ID {doc.CaseID})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document ID {DocumentID}", documentId);
                return false;
            }
        }

        public static bool OpenDocument(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return false;

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening document: {ex.Message}");
                return false;
            }
        }
    }
}
