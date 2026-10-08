using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LawCaseManagement.Core
{
    /// <summary>
    /// Helper for provisioning SQL Server logins, database roles, and per-user permissions.
    /// 
    /// Implements §6 requirements:
    /// - Per-employee SQL Server logins (not shared credentials)
    /// - Principle of least privilege with custom roles per user type
    /// - db_datareader/db_datawriter or custom roles per role (Admin/Lawyer/Paralegal)
    /// 
    /// Must be run by a user with sysadmin or securityadmin privileges on the SQL Server instance.
    /// </summary>
    public class SqlServerSetupHelper
    {
        private readonly string _masterConnectionString;
        private readonly string _databaseName;
        private readonly ILogger<SqlServerSetupHelper> _logger;

        /// <summary>
        /// Initializes the setup helper.
        /// </summary>
        /// <param name="masterConnectionString">
        /// Connection string to the 'master' database with sysadmin privileges.
        /// Example: "Server=192.168.1.100;Database=master;User Id=sa;Password=...;Encrypt=True;TrustServerCertificate=True"
        /// </param>
        /// <param name="databaseName">The target database name (e.g., "LawCaseManagement").</param>
        /// <param name="logger">Optional logger.</param>
        public SqlServerSetupHelper(
            string masterConnectionString,
            string databaseName = "LawCaseManagement",
            ILogger<SqlServerSetupHelper>? logger = null)
        {
            _masterConnectionString = masterConnectionString;
            _databaseName = databaseName;
            _logger = logger ?? NullLogger<SqlServerSetupHelper>.Instance;
        }

        /// <summary>
        /// Creates the target database if it doesn't already exist.
        /// </summary>
        public void EnsureDatabaseExists()
        {
            _logger.LogInformation("Ensuring database '{Database}' exists...", _databaseName);

            using var conn = new SqlConnection(_masterConnectionString);
            conn.Open();

            string checkSql = $"SELECT DB_ID('{_databaseName}')";
            using var checkCmd = new SqlCommand(checkSql, conn);
            var result = checkCmd.ExecuteScalar();

            if (result == DBNull.Value || result == null)
            {
                string createSql = $"CREATE DATABASE [{_databaseName}]";
                using var createCmd = new SqlCommand(createSql, conn);
                createCmd.ExecuteNonQuery();
                _logger.LogInformation("Database '{Database}' created successfully.", _databaseName);
            }
            else
            {
                _logger.LogInformation("Database '{Database}' already exists.", _databaseName);
            }
        }

        /// <summary>
        /// Creates custom database roles for the law firm's role-based access control.
        /// - LawFirm_Admin: Full data access + can manage users/audit logs
        /// - LawFirm_Lawyer: Read/write on cases, clients, tasks, documents
        /// - LawFirm_Paralegal: Read on cases/clients, read/write on tasks, limited document access
        /// </summary>
        public void CreateDatabaseRoles()
        {
            _logger.LogInformation("Creating custom database roles...");

            string dbConnectionString = _masterConnectionString
                .Replace("Database=master", $"Database={_databaseName}", StringComparison.OrdinalIgnoreCase);

            using var conn = new SqlConnection(dbConnectionString);
            conn.Open();

            // Create roles if they don't exist
            string[] roles = { "LawFirm_Admin", "LawFirm_Lawyer", "LawFirm_Paralegal" };
            foreach (var role in roles)
            {
                ExecuteIfNotExists(conn,
                    $"SELECT 1 FROM sys.database_principals WHERE name = '{role}' AND type = 'R'",
                    $"CREATE ROLE [{role}]",
                    $"Role '{role}'");
            }

            // Admin: full data access
            ExecuteSql(conn, @"
                GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [LawFirm_Admin];
            ");

            // Lawyer: read/write on core tables
            string[] lawyerTables = { "Cases", "Clients", "CaseClients", "Tasks", "Documents", "AuditLogs" };
            foreach (var table in lawyerTables)
            {
                ExecuteSql(conn, $"GRANT SELECT, INSERT, UPDATE ON [{table}] TO [LawFirm_Lawyer];");
            }
            ExecuteSql(conn, "GRANT SELECT ON [Users] TO [LawFirm_Lawyer];");
            ExecuteSql(conn, "GRANT DELETE ON [Documents] TO [LawFirm_Lawyer];");
            ExecuteSql(conn, "GRANT DELETE ON [Tasks] TO [LawFirm_Lawyer];");

            // Paralegal: more restricted
            string[] paralegalReadTables = { "Cases", "Clients", "CaseClients", "Users" };
            foreach (var table in paralegalReadTables)
            {
                ExecuteSql(conn, $"GRANT SELECT ON [{table}] TO [LawFirm_Paralegal];");
            }
            ExecuteSql(conn, "GRANT SELECT, INSERT, UPDATE ON [Tasks] TO [LawFirm_Paralegal];");
            ExecuteSql(conn, "GRANT SELECT, INSERT ON [Documents] TO [LawFirm_Paralegal];");
            ExecuteSql(conn, "GRANT SELECT, INSERT ON [AuditLogs] TO [LawFirm_Paralegal];");
            ExecuteSql(conn, "GRANT UPDATE ON [Cases] TO [LawFirm_Paralegal];"); // For status updates on assigned cases

            // Sync tables: all roles need access
            string[] syncTables = { "SyncChangeLogs", "SyncConflicts" };
            foreach (var table in syncTables)
            {
                ExecuteSql(conn, $"GRANT SELECT, INSERT, UPDATE ON [{table}] TO [LawFirm_Admin];");
                ExecuteSql(conn, $"GRANT SELECT, INSERT, UPDATE ON [{table}] TO [LawFirm_Lawyer];");
                ExecuteSql(conn, $"GRANT SELECT, INSERT, UPDATE ON [{table}] TO [LawFirm_Paralegal];");
            }

            _logger.LogInformation("Database roles created and permissions granted.");
        }

        /// <summary>
        /// Creates a SQL Server login and database user for an employee.
        /// Maps the user to the appropriate database role based on their app role.
        /// </summary>
        /// <param name="loginName">SQL Server login name (e.g., employee's username).</param>
        /// <param name="password">SQL Server login password.</param>
        /// <param name="appRole">Application role: "Admin", "Lawyer", or "Paralegal".</param>
        /// <returns>The connection string for this user.</returns>
        public string CreateEmployeeLogin(string loginName, string password, string appRole)
        {
            _logger.LogInformation("Creating SQL Server login for '{LoginName}' (Role: {Role})...", loginName, appRole);

            // Step 1: Create server-level login on master
            using (var masterConn = new SqlConnection(_masterConnectionString))
            {
                masterConn.Open();
                ExecuteIfNotExists(masterConn,
                    $"SELECT 1 FROM sys.server_principals WHERE name = '{loginName}'",
                    $"CREATE LOGIN [{loginName}] WITH PASSWORD = '{EscapeSqlString(password)}', CHECK_POLICY = OFF",
                    $"Login '{loginName}'");
            }

            // Step 2: Create database user and assign to role
            string dbConnectionString = _masterConnectionString
                .Replace("Database=master", $"Database={_databaseName}", StringComparison.OrdinalIgnoreCase);

            using (var dbConn = new SqlConnection(dbConnectionString))
            {
                dbConn.Open();

                ExecuteIfNotExists(dbConn,
                    $"SELECT 1 FROM sys.database_principals WHERE name = '{loginName}'",
                    $"CREATE USER [{loginName}] FOR LOGIN [{loginName}]",
                    $"Database user '{loginName}'");

                // Map to the correct role
                string dbRole = appRole switch
                {
                    "Admin" => "LawFirm_Admin",
                    "Lawyer" => "LawFirm_Lawyer",
                    "Paralegal" => "LawFirm_Paralegal",
                    _ => "LawFirm_Paralegal" // Default to least privilege
                };

                ExecuteSql(dbConn, $"ALTER ROLE [{dbRole}] ADD MEMBER [{loginName}];");
            }

            // Build the user-specific connection string
            var builder = new SqlConnectionStringBuilder(_masterConnectionString)
            {
                InitialCatalog = _databaseName,
                UserID = loginName,
                Password = password,
                Encrypt = true,
                TrustServerCertificate = true
            };

            _logger.LogInformation("Login '{LoginName}' created and assigned to role for '{Role}'.", loginName, appRole);
            return builder.ConnectionString;
        }

        /// <summary>
        /// Generates the SQL Server setup script as a string for manual review/execution.
        /// Useful for DBAs who prefer to run setup scripts manually.
        /// </summary>
        /// <param name="employees">List of (loginName, password, appRole) tuples.</param>
        /// <returns>Complete T-SQL setup script.</returns>
        public string GenerateSetupScript(List<(string LoginName, string Password, string AppRole)> employees)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("-- ═══════════════════════════════════════════════════════════════════════");
            sb.AppendLine("-- Law Case Management System — SQL Server Setup Script");
            sb.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine("-- ═══════════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("-- Step 1: Create Database");
            sb.AppendLine($"IF DB_ID('{_databaseName}') IS NULL");
            sb.AppendLine($"    CREATE DATABASE [{_databaseName}];");
            sb.AppendLine("GO");
            sb.AppendLine();
            sb.AppendLine($"USE [{_databaseName}];");
            sb.AppendLine("GO");
            sb.AppendLine();
            sb.AppendLine("-- Step 2: Create Custom Database Roles");
            foreach (var role in new[] { "LawFirm_Admin", "LawFirm_Lawyer", "LawFirm_Paralegal" })
            {
                sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '{role}' AND type = 'R')");
                sb.AppendLine($"    CREATE ROLE [{role}];");
            }
            sb.AppendLine("GO");
            sb.AppendLine();
            sb.AppendLine("-- Step 3: Grant Permissions");
            sb.AppendLine("GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [LawFirm_Admin];");
            sb.AppendLine();
            foreach (var table in new[] { "Cases", "Clients", "CaseClients", "Tasks", "Documents", "AuditLogs" })
            {
                sb.AppendLine($"GRANT SELECT, INSERT, UPDATE ON [{table}] TO [LawFirm_Lawyer];");
            }
            sb.AppendLine("GRANT SELECT ON [Users] TO [LawFirm_Lawyer];");
            sb.AppendLine("GRANT DELETE ON [Documents] TO [LawFirm_Lawyer];");
            sb.AppendLine("GRANT DELETE ON [Tasks] TO [LawFirm_Lawyer];");
            sb.AppendLine();
            foreach (var table in new[] { "Cases", "Clients", "CaseClients", "Users" })
            {
                sb.AppendLine($"GRANT SELECT ON [{table}] TO [LawFirm_Paralegal];");
            }
            sb.AppendLine("GRANT SELECT, INSERT, UPDATE ON [Tasks] TO [LawFirm_Paralegal];");
            sb.AppendLine("GRANT SELECT, INSERT ON [Documents] TO [LawFirm_Paralegal];");
            sb.AppendLine("GRANT SELECT, INSERT ON [AuditLogs] TO [LawFirm_Paralegal];");
            sb.AppendLine("GRANT UPDATE ON [Cases] TO [LawFirm_Paralegal];");
            sb.AppendLine("GO");
            sb.AppendLine();
            sb.AppendLine("-- Step 4: Create Employee Logins");
            foreach (var (loginName, password, appRole) in employees)
            {
                string dbRole = appRole switch
                {
                    "Admin" => "LawFirm_Admin",
                    "Lawyer" => "LawFirm_Lawyer",
                    _ => "LawFirm_Paralegal"
                };

                sb.AppendLine($"-- Employee: {loginName} ({appRole})");
                sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = '{loginName}')");
                sb.AppendLine($"    CREATE LOGIN [{loginName}] WITH PASSWORD = '{EscapeSqlString(password)}', CHECK_POLICY = OFF;");
                sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '{loginName}')");
                sb.AppendLine($"    CREATE USER [{loginName}] FOR LOGIN [{loginName}];");
                sb.AppendLine($"ALTER ROLE [{dbRole}] ADD MEMBER [{loginName}];");
                sb.AppendLine();
            }

            sb.AppendLine("GO");
            sb.AppendLine("PRINT 'Setup complete.';");

            return sb.ToString();
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private void ExecuteSql(SqlConnection conn, string sql)
        {
            try
            {
                using var cmd = new SqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            catch (SqlException ex)
            {
                _logger.LogWarning(ex, "SQL Warning (non-fatal): {Sql}", sql.Trim());
            }
        }

        private void ExecuteIfNotExists(SqlConnection conn, string checkSql, string createSql, string description)
        {
            using var checkCmd = new SqlCommand(checkSql, conn);
            var result = checkCmd.ExecuteScalar();

            if (result == null || result == DBNull.Value)
            {
                using var createCmd = new SqlCommand(createSql, conn);
                createCmd.ExecuteNonQuery();
                _logger.LogInformation("{Description} created.", description);
            }
            else
            {
                _logger.LogInformation("{Description} already exists.", description);
            }
        }

        private static string EscapeSqlString(string input) => input.Replace("'", "''");
    }
}
