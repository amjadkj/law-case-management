using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LawCaseManagement.Core
{
    /// <summary>
    /// One-time data migration tool for exporting data from an existing SQLite database
    /// and importing it into the new central SQL Server database.
    /// 
    /// Implements §5.2: "Write a migration script/tool to export existing per-user SQLite
    /// data and import into the new central SQL Server database."
    /// 
    /// This tool:
    /// 1. Reads all data from a source SQLite database
    /// 2. Writes it to the target SQL Server database
    /// 3. Handles ID remapping (SQLite IDs → new SQL Server IDs)
    /// 4. Provides a report of migrated records and any issues
    /// 
    /// IMPORTANT: Run this tool only once per source SQLite database, BEFORE going live.
    /// Review the migration report for duplicates (per §5.2 recommendation).
    /// </summary>
    public class DataMigrationTool
    {
        private readonly ILogger<DataMigrationTool> _logger;

        public DataMigrationTool(ILogger<DataMigrationTool>? logger = null)
        {
            _logger = logger ?? NullLogger<DataMigrationTool>.Instance;
        }

        /// <summary>
        /// Result of a migration operation.
        /// </summary>
        public class MigrationResult
        {
            public bool Success { get; set; }
            public int UsersImported { get; set; }
            public int ClientsImported { get; set; }
            public int CasesImported { get; set; }
            public int CaseClientLinksImported { get; set; }
            public int TasksImported { get; set; }
            public int DocumentsImported { get; set; }
            public int AuditLogsImported { get; set; }
            public List<string> Warnings { get; set; } = new();
            public List<string> Errors { get; set; } = new();
            public Dictionary<string, Dictionary<int, int>> IdMappings { get; set; } = new();
        }

        /// <summary>
        /// Migrates all data from the source SQLite database to the target SQL Server database.
        /// </summary>
        /// <param name="sqliteConnectionString">Connection string for the source SQLite database.</param>
        /// <param name="sqlServerConnectionString">Connection string for the target SQL Server database.</param>
        /// <param name="skipExistingUsers">
        /// If true, users with matching usernames in SQL Server will be skipped (not overwritten).
        /// Their existing SQL Server ID will be used for ID remapping.
        /// </param>
        /// <returns>Migration result with counts and any warnings.</returns>
        public MigrationResult Migrate(
            string sqliteConnectionString,
            string sqlServerConnectionString,
            bool skipExistingUsers = true)
        {
            var result = new MigrationResult();

            try
            {
                _logger.LogInformation("Starting data migration from SQLite to SQL Server...");
                _logger.LogInformation("Source: {Source}", sqliteConnectionString);

                // Build source context (SQLite)
                var sourceOptions = new DbContextOptionsBuilder<CaseDbContext>()
                    .UseSqlite(sqliteConnectionString)
                    .Options;

                // Build target context (SQL Server)
                var targetOptions = new DbContextOptionsBuilder<CaseDbContext>()
                    .UseSqlServer(sqlServerConnectionString)
                    .Options;

                using var sourceDb = new CaseDbContext(sourceOptions);
                using var targetDb = new CaseDbContext(targetOptions);

                // Ensure target schema exists
                _logger.LogInformation("Ensuring SQL Server schema is up to date...");
                targetDb.Database.EnsureCreated();

                // ID mapping dictionaries (old SQLite ID → new SQL Server ID)
                var userIdMap = new Dictionary<int, int>();
                var clientIdMap = new Dictionary<int, int>();
                var caseIdMap = new Dictionary<int, int>();

                result.IdMappings["Users"] = userIdMap;
                result.IdMappings["Clients"] = clientIdMap;
                result.IdMappings["Cases"] = caseIdMap;

                // ── Step 1: Migrate Users ────────────────────────────────────────
                _logger.LogInformation("Migrating Users...");
                var sourceUsers = sourceDb.Users.AsNoTracking().ToList();

                foreach (var user in sourceUsers)
                {
                    int oldId = user.UserID;

                    // Check for existing user with same username
                    var existingUser = targetDb.Users
                        .FirstOrDefault(u => u.Username.ToLower() == user.Username.ToLower());

                    if (existingUser != null)
                    {
                        if (skipExistingUsers)
                        {
                            userIdMap[oldId] = existingUser.UserID;
                            result.Warnings.Add($"User '{user.Username}' already exists in SQL Server (ID {existingUser.UserID}). Skipped.");
                            continue;
                        }
                    }

                    // Reset ID for SQL Server identity insert
                    user.UserID = 0;
                    targetDb.Users.Add(user);
                    targetDb.SaveChanges();

                    userIdMap[oldId] = user.UserID;
                    result.UsersImported++;
                }

                _logger.LogInformation("Users migrated: {Count}", result.UsersImported);

                // ── Step 2: Migrate Clients ──────────────────────────────────────
                _logger.LogInformation("Migrating Clients...");
                var sourceClients = sourceDb.Clients.AsNoTracking().ToList();

                foreach (var client in sourceClients)
                {
                    int oldId = client.ClientID;

                    // Check for potential duplicates by name + phone
                    var existingClient = targetDb.Clients
                        .FirstOrDefault(c => c.FullName.ToLower() == client.FullName.ToLower()
                                          && c.Phone == client.Phone);

                    if (existingClient != null)
                    {
                        clientIdMap[oldId] = existingClient.ClientID;
                        result.Warnings.Add($"Client '{client.FullName}' (Phone: {client.Phone}) appears to be a duplicate. Mapped to existing ID {existingClient.ClientID}.");
                        continue;
                    }

                    client.ClientID = 0;
                    client.LastModifiedUtc = DateTime.UtcNow;
                    targetDb.Clients.Add(client);
                    targetDb.SaveChanges();

                    clientIdMap[oldId] = client.ClientID;
                    result.ClientsImported++;
                }

                _logger.LogInformation("Clients migrated: {Count}", result.ClientsImported);

                // ── Step 3: Migrate Cases ────────────────────────────────────────
                _logger.LogInformation("Migrating Cases...");
                var sourceCases = sourceDb.Cases.AsNoTracking().ToList();

                foreach (var caseRecord in sourceCases)
                {
                    int oldId = caseRecord.CaseID;

                    // Check for duplicate FileNumber
                    var existingCase = targetDb.Cases
                        .FirstOrDefault(c => c.FileNumber == caseRecord.FileNumber);

                    if (existingCase != null)
                    {
                        caseIdMap[oldId] = existingCase.CaseID;
                        result.Warnings.Add($"Case '{caseRecord.FileNumber}' already exists. Mapped to existing ID {existingCase.CaseID}.");
                        continue;
                    }

                    // Remap foreign keys
                    if (userIdMap.TryGetValue(caseRecord.LawyerID, out int newLawyerId))
                        caseRecord.LawyerID = newLawyerId;
                    else
                        result.Warnings.Add($"Case '{caseRecord.FileNumber}': LawyerID {caseRecord.LawyerID} not found in mapping.");

                    if (userIdMap.TryGetValue(caseRecord.ParalegalID, out int newParalegalId))
                        caseRecord.ParalegalID = newParalegalId;
                    else
                        result.Warnings.Add($"Case '{caseRecord.FileNumber}': ParalegalID {caseRecord.ParalegalID} not found in mapping.");

                    caseRecord.CaseID = 0;
                    caseRecord.LastModifiedUtc = DateTime.UtcNow;
                    targetDb.Cases.Add(caseRecord);
                    targetDb.SaveChanges();

                    caseIdMap[oldId] = caseRecord.CaseID;
                    result.CasesImported++;
                }

                _logger.LogInformation("Cases migrated: {Count}", result.CasesImported);

                // ── Step 4: Migrate CaseClient Links ─────────────────────────────
                _logger.LogInformation("Migrating CaseClient links...");
                var sourceCaseClients = sourceDb.CaseClients.AsNoTracking().ToList();

                foreach (var link in sourceCaseClients)
                {
                    int newCaseId = caseIdMap.GetValueOrDefault(link.CaseID, 0);
                    int newClientId = clientIdMap.GetValueOrDefault(link.ClientID, 0);

                    if (newCaseId == 0 || newClientId == 0)
                    {
                        result.Warnings.Add($"CaseClient link (Case {link.CaseID} → Client {link.ClientID}) skipped: missing ID mapping.");
                        continue;
                    }

                    // Check for existing link
                    bool exists = targetDb.CaseClients.Any(cc => cc.CaseID == newCaseId && cc.ClientID == newClientId);
                    if (exists) continue;

                    targetDb.CaseClients.Add(new CaseClient { CaseID = newCaseId, ClientID = newClientId });
                    targetDb.SaveChanges();
                    result.CaseClientLinksImported++;
                }

                _logger.LogInformation("CaseClient links migrated: {Count}", result.CaseClientLinksImported);

                // ── Step 5: Migrate Tasks ────────────────────────────────────────
                _logger.LogInformation("Migrating Tasks...");
                var sourceTasks = sourceDb.Tasks.AsNoTracking().ToList();

                foreach (var task in sourceTasks)
                {
                    int newCaseId = caseIdMap.GetValueOrDefault(task.CaseID, 0);
                    int newAssignedToId = userIdMap.GetValueOrDefault(task.AssignedToID, 0);

                    if (newCaseId == 0)
                    {
                        result.Warnings.Add($"Task '{task.Title}' skipped: CaseID {task.CaseID} not mapped.");
                        continue;
                    }

                    if (newAssignedToId == 0)
                    {
                        result.Warnings.Add($"Task '{task.Title}': AssignedToID {task.AssignedToID} not mapped. Using first available user.");
                        newAssignedToId = targetDb.Users.First().UserID;
                    }

                    task.TaskID = 0;
                    task.CaseID = newCaseId;
                    task.AssignedToID = newAssignedToId;
                    task.LastModifiedUtc = DateTime.UtcNow;

                    targetDb.Tasks.Add(task);
                    targetDb.SaveChanges();
                    result.TasksImported++;
                }

                _logger.LogInformation("Tasks migrated: {Count}", result.TasksImported);

                // ── Step 6: Migrate Documents (metadata only — files stay on disk) ─
                _logger.LogInformation("Migrating Document records...");
                var sourceDocs = sourceDb.Documents.AsNoTracking().ToList();

                foreach (var doc in sourceDocs)
                {
                    int newCaseId = caseIdMap.GetValueOrDefault(doc.CaseID, 0);
                    int newUploadedById = userIdMap.GetValueOrDefault(doc.UploadedByID, 0);

                    if (newCaseId == 0)
                    {
                        result.Warnings.Add($"Document '{doc.FileName}' skipped: CaseID {doc.CaseID} not mapped.");
                        continue;
                    }

                    if (newUploadedById == 0)
                    {
                        result.Warnings.Add($"Document '{doc.FileName}': UploadedByID {doc.UploadedByID} not mapped. Using first available user.");
                        newUploadedById = targetDb.Users.First().UserID;
                    }

                    doc.DocumentID = 0;
                    doc.CaseID = newCaseId;
                    doc.UploadedByID = newUploadedById;
                    doc.LastModifiedUtc = DateTime.UtcNow;

                    targetDb.Documents.Add(doc);
                    targetDb.SaveChanges();
                    result.DocumentsImported++;
                }

                _logger.LogInformation("Documents migrated: {Count}", result.DocumentsImported);

                // ── Step 7: Migrate Audit Logs ───────────────────────────────────
                _logger.LogInformation("Migrating Audit Logs...");
                var sourceAuditLogs = sourceDb.AuditLogs.AsNoTracking().ToList();

                foreach (var log in sourceAuditLogs)
                {
                    if (log.UserID.HasValue)
                    {
                        int newUserId = userIdMap.GetValueOrDefault(log.UserID.Value, 0);
                        log.UserID = newUserId > 0 ? newUserId : null;
                    }

                    log.LogID = 0;
                    targetDb.AuditLogs.Add(log);
                    targetDb.SaveChanges();
                    result.AuditLogsImported++;
                }

                _logger.LogInformation("Audit Logs migrated: {Count}", result.AuditLogsImported);

                result.Success = true;
                _logger.LogInformation("═══ Migration Complete ═══");
                _logger.LogInformation("  Users: {Users}, Clients: {Clients}, Cases: {Cases}",
                    result.UsersImported, result.ClientsImported, result.CasesImported);
                _logger.LogInformation("  Links: {Links}, Tasks: {Tasks}, Documents: {Documents}, Audit Logs: {Logs}",
                    result.CaseClientLinksImported, result.TasksImported, result.DocumentsImported, result.AuditLogsImported);
                _logger.LogInformation("  Warnings: {WarningCount}", result.Warnings.Count);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Migration failed: {ex.Message}");
                _logger.LogError(ex, "Data migration failed.");
            }

            return result;
        }

        /// <summary>
        /// Generates a migration report suitable for manual review.
        /// </summary>
        public static string GenerateReport(MigrationResult result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  DATA MIGRATION REPORT — SQLite → SQL Server");
            sb.AppendLine($"  Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"  Overall Status: {(result.Success ? "✓ SUCCESS" : "✗ FAILED")}");
            sb.AppendLine();
            sb.AppendLine("  Records Migrated:");
            sb.AppendLine($"    Users:            {result.UsersImported}");
            sb.AppendLine($"    Clients:          {result.ClientsImported}");
            sb.AppendLine($"    Cases:            {result.CasesImported}");
            sb.AppendLine($"    Case-Client Links: {result.CaseClientLinksImported}");
            sb.AppendLine($"    Tasks:            {result.TasksImported}");
            sb.AppendLine($"    Documents:        {result.DocumentsImported}");
            sb.AppendLine($"    Audit Logs:       {result.AuditLogsImported}");
            sb.AppendLine();

            if (result.Warnings.Count > 0)
            {
                sb.AppendLine($"  ⚠ Warnings ({result.Warnings.Count}):");
                foreach (var warning in result.Warnings)
                {
                    sb.AppendLine($"    - {warning}");
                }
                sb.AppendLine();
            }

            if (result.Errors.Count > 0)
            {
                sb.AppendLine($"  ✗ Errors ({result.Errors.Count}):");
                foreach (var error in result.Errors)
                {
                    sb.AppendLine($"    - {error}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("  ID Mappings (SQLite → SQL Server):");
            foreach (var (entityType, mappings) in result.IdMappings)
            {
                sb.AppendLine($"    {entityType}: {mappings.Count} records mapped");
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  IMPORTANT: Review this report for duplicates before going live.");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            return sb.ToString();
        }
    }
}
