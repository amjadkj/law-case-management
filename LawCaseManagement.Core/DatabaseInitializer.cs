using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LawCaseManagement.Core
{
    /// <summary>
    /// Handles database creation and initial seed data.
    /// Called once at application startup — never from a ViewModel.
    /// </summary>
    public class DatabaseInitializer
    {
        private readonly CaseDbContext _db;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(CaseDbContext db, ILogger<DatabaseInitializer> logger)
        {
            _db = db;
            _logger = logger;
        }

        public void Initialize()
        {
            try
            {
                _logger.LogInformation("Applying database migrations...");
                _db.Database.EnsureCreated(); // Use Migrate() once migrations are generated
                _logger.LogInformation("Database ready.");

                SeedIfEmpty();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database initialization failed.");
                throw;
            }
        }

        private void SeedIfEmpty()
        {
            if (_db.Users.Any())
            {
                _logger.LogInformation("Database already contains data — skipping seed.");
                return;
            }

            _logger.LogInformation("Seeding initial data...");

            // ── Users ─────────────────────────────────────────────────────────
            var admin = new User
            {
                FullName = "System Administrator",
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = Roles.Admin,
                IsActive = true
            };

            var lawyer1 = new User
            {
                FullName = "Sarah Jenkins (Attorney)",
                Username = "sarah",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("lawyer123"),
                Role = Roles.Lawyer,
                IsActive = true
            };

            var lawyer2 = new User
            {
                FullName = "Michael Ross (Attorney)",
                Username = "michael",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("lawyer123"),
                Role = Roles.Lawyer,
                IsActive = true
            };

            var paralegal1 = new User
            {
                FullName = "Rachel Zane (Paralegal)",
                Username = "rachel",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("paralegal123"),
                Role = Roles.Paralegal,
                IsActive = true
            };

            _db.Users.AddRange(admin, lawyer1, lawyer2, paralegal1);
            _db.SaveChanges();

            // ── Clients ───────────────────────────────────────────────────────
            var client1 = new Client
            {
                FullName = "Apex Global Industries LLC",
                Address = "100 Financial District, New York, NY",
                Phone = "555-0192",
                Email = "legal@apexglobal.com"
            };

            var client2 = new Client
            {
                FullName = "Robert J. Vance",
                Address = "742 Evergreen Terrace, Springfield",
                Phone = "555-4819",
                Email = "robert.vance@gmail.com"
            };

            _db.Clients.AddRange(client1, client2);
            _db.SaveChanges();

            // ── Cases ─────────────────────────────────────────────────────────
            var case1 = new Case
            {
                FileNumber = "CIV-2026-0001",
                Title = "Apex Global vs. Omega Corp Patent Dispute",
                CaseType = CaseTypes.Corporate,
                Status = CaseStatuses.InProgress,
                OpenDate = DateTime.Today.AddDays(-30),
                LawyerID = lawyer1.UserID,
                ParalegalID = paralegal1.UserID,
                Notes = "Patent infringement suit regarding intellectual property in automated systems."
            };

            var case2 = new Case
            {
                FileNumber = "CRM-2026-0084",
                Title = "State of New York vs. Robert Vance",
                CaseType = CaseTypes.Criminal,
                Status = CaseStatuses.Open,
                OpenDate = DateTime.Today.AddDays(-5),
                LawyerID = lawyer2.UserID,
                ParalegalID = paralegal1.UserID,
                Notes = "Defense case regarding traffic violations and property damage."
            };

            _db.Cases.AddRange(case1, case2);
            _db.SaveChanges();

            // ── Case–Client links ─────────────────────────────────────────────
            _db.CaseClients.Add(new CaseClient { CaseID = case1.CaseID, ClientID = client1.ClientID });
            _db.CaseClients.Add(new CaseClient { CaseID = case2.CaseID, ClientID = client2.ClientID });
            _db.SaveChanges();

            // ── Tasks ─────────────────────────────────────────────────────────
            _db.Tasks.AddRange(
                new Task
                {
                    CaseID = case1.CaseID,
                    Title = "Draft response to motion to dismiss",
                    AssignedToID = paralegal1.UserID,
                    DueDate = DateTime.Today.AddDays(3),
                    Status = TaskStatuses.Pending
                },
                new Task
                {
                    CaseID = case1.CaseID,
                    Title = "Gather documentation on patent patent-X12",
                    AssignedToID = paralegal1.UserID,
                    DueDate = DateTime.Today.AddDays(-2),
                    Status = TaskStatuses.Completed
                },
                new Task
                {
                    CaseID = case2.CaseID,
                    Title = "Interview defense witness Mr. Simpson",
                    AssignedToID = lawyer2.UserID,
                    DueDate = DateTime.Today.AddDays(7),
                    Status = TaskStatuses.InProgress
                }
            );
            _db.SaveChanges();

            // ── Audit log ─────────────────────────────────────────────────────
            _db.AuditLogs.Add(new AuditLog
            {
                UserID = admin.UserID,
                Action = "System Initialized",
                Details = "Database seeded with initial user accounts, clients, and mock cases.",
                Timestamp = DateTime.UtcNow
            });
            _db.SaveChanges();

            _logger.LogInformation("Seed data applied successfully.");
        }
    }
}
