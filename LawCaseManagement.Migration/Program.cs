using System;
using System.Collections.Generic;
using System.IO;
using LawCaseManagement.Core;
using Microsoft.Extensions.Logging;

namespace LawCaseManagement.Migration
{
    /// <summary>
    /// Command-line tool for:
    /// 1. Setting up SQL Server (create database, roles, employee logins)
    /// 2. Migrating data from SQLite to SQL Server
    /// 3. Encrypting connection strings for secure storage
    /// 4. Generating SQL Server setup scripts for DBA review
    /// 
    /// Usage:
    ///   dotnet run --project LawCaseManagement.Migration -- setup     [masterConnStr]
    ///   dotnet run --project LawCaseManagement.Migration -- migrate   [sqliteConnStr] [sqlServerConnStr]
    ///   dotnet run --project LawCaseManagement.Migration -- encrypt   [connectionString]
    ///   dotnet run --project LawCaseManagement.Migration -- decrypt   [encryptedString]
    ///   dotnet run --project LawCaseManagement.Migration -- script    [masterConnStr]
    /// </summary>
    internal class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("  Law Case Management — Migration & Setup Tool");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine();

            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            string command = args[0].ToLower();

            try
            {
                return command switch
                {
                    "setup" => RunSetup(args),
                    "migrate" => RunMigration(args),
                    "encrypt" => RunEncrypt(args),
                    "decrypt" => RunDecrypt(args),
                    "script" => RunGenerateScript(args),
                    "help" or "--help" or "-h" => PrintUsageAndReturn(),
                    _ => PrintUnknownCommand(command)
                };
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n✗ Error: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine($"\nStack trace: {ex.StackTrace}");
                return 1;
            }
        }

        // ── SETUP command ────────────────────────────────────────────────────
        static int RunSetup(string[] args)
        {
            Console.WriteLine("  Mode: SQL Server Setup");
            Console.WriteLine("  ─────────────────────────");
            Console.WriteLine();

            string masterConnStr;
            if (args.Length > 1)
            {
                masterConnStr = args[1];
            }
            else
            {
                Console.Write("  Enter SQL Server master connection string\n  (e.g., Server=192.168.1.100;Database=master;User Id=sa;Password=YourPassword;Encrypt=True;TrustServerCertificate=True)\n  > ");
                masterConnStr = Console.ReadLine()?.Trim() ?? "";
            }

            if (string.IsNullOrWhiteSpace(masterConnStr))
            {
                Console.WriteLine("  ✗ Connection string cannot be empty.");
                return 1;
            }

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<SqlServerSetupHelper>();
            var setup = new SqlServerSetupHelper(masterConnStr, "LawCaseManagement", logger);

            Console.WriteLine("\n  Step 1: Creating database...");
            setup.EnsureDatabaseExists();

            Console.WriteLine("  Step 2: Creating database roles...");
            setup.CreateDatabaseRoles();

            Console.WriteLine("\n  Step 3: Creating employee logins...");
            Console.WriteLine("  Enter employee details (empty login name to finish):");

            var employees = new List<(string, string, string)>();
            while (true)
            {
                Console.Write("\n    Login name: ");
                string loginName = Console.ReadLine()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(loginName)) break;

                Console.Write("    Password: ");
                string password = ReadPassword();

                Console.Write("    Role (Admin/Lawyer/Paralegal): ");
                string role = Console.ReadLine()?.Trim() ?? "Paralegal";

                string connStr = setup.CreateEmployeeLogin(loginName, password, role);
                employees.Add((loginName, password, role));

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"    ✓ Login '{loginName}' created.");
                Console.ResetColor();

                // Offer to encrypt the connection string
                Console.Write("    Encrypt connection string for this user? (y/n): ");
                if (Console.ReadLine()?.Trim().ToLower() == "y")
                {
                    string encrypted = CredentialProtector.Encrypt(connStr);
                    Console.WriteLine($"    Encrypted: {encrypted}");
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n  ✓ SQL Server setup complete!");
            Console.ResetColor();
            return 0;
        }

        // ── MIGRATE command ──────────────────────────────────────────────────
        static int RunMigration(string[] args)
        {
            Console.WriteLine("  Mode: Data Migration (SQLite → SQL Server)");
            Console.WriteLine("  ─────────────────────────────────────────────");
            Console.WriteLine();

            string sqliteConnStr;
            string sqlServerConnStr;

            if (args.Length >= 3)
            {
                sqliteConnStr = args[1];
                sqlServerConnStr = args[2];
            }
            else
            {
                Console.Write("  Path to source SQLite database file\n  (e.g., C:\\Users\\john\\law_case_management.db)\n  > ");
                string dbPath = Console.ReadLine()?.Trim() ?? "";

                if (!File.Exists(dbPath))
                {
                    Console.WriteLine($"  ✗ File not found: {dbPath}");
                    return 1;
                }

                sqliteConnStr = $"Data Source={dbPath}";

                Console.Write("\n  Target SQL Server connection string\n  (e.g., Server=192.168.1.100;Database=LawCaseManagement;User Id=sa;Password=...;Encrypt=True;TrustServerCertificate=True)\n  > ");
                sqlServerConnStr = Console.ReadLine()?.Trim() ?? "";
            }

            if (string.IsNullOrWhiteSpace(sqliteConnStr) || string.IsNullOrWhiteSpace(sqlServerConnStr))
            {
                Console.WriteLine("  ✗ Both connection strings are required.");
                return 1;
            }

            // Try to decrypt if encrypted
            sqlServerConnStr = CredentialProtector.DecryptOrPassthrough(sqlServerConnStr);

            Console.WriteLine("\n  ⚠ WARNING: This will import data into the SQL Server database.");
            Console.Write("  Continue? (y/n): ");
            if (Console.ReadLine()?.Trim().ToLower() != "y")
            {
                Console.WriteLine("  Aborted.");
                return 0;
            }

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<DataMigrationTool>();
            var migrator = new DataMigrationTool(logger);

            Console.WriteLine("\n  Running migration...\n");
            var result = migrator.Migrate(sqliteConnStr, sqlServerConnStr);

            // Print report
            string report = DataMigrationTool.GenerateReport(result);
            Console.WriteLine(report);

            // Save report to file
            string reportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LawCaseManagement",
                $"migration_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            File.WriteAllText(reportPath, report);
            Console.WriteLine($"  Report saved to: {reportPath}");

            return result.Success ? 0 : 1;
        }

        // ── ENCRYPT command ──────────────────────────────────────────────────
        static int RunEncrypt(string[] args)
        {
            Console.WriteLine("  Mode: Encrypt Connection String (DPAPI)");
            Console.WriteLine("  ────────────────────────────────────────");
            Console.WriteLine();

            string plainText;
            if (args.Length > 1)
            {
                plainText = args[1];
            }
            else
            {
                Console.Write("  Enter connection string to encrypt:\n  > ");
                plainText = Console.ReadLine()?.Trim() ?? "";
            }

            if (string.IsNullOrWhiteSpace(plainText))
            {
                Console.WriteLine("  ✗ Input cannot be empty.");
                return 1;
            }

            string encrypted = CredentialProtector.Encrypt(plainText);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  ✓ Encrypted (copy this to appsettings.json):");
            Console.ResetColor();
            Console.WriteLine($"  {encrypted}");

            return 0;
        }

        // ── DECRYPT command ──────────────────────────────────────────────────
        static int RunDecrypt(string[] args)
        {
            Console.WriteLine("  Mode: Decrypt Connection String (DPAPI)");
            Console.WriteLine("  ────────────────────────────────────────");
            Console.WriteLine();

            string encrypted;
            if (args.Length > 1)
            {
                encrypted = args[1];
            }
            else
            {
                Console.Write("  Enter encrypted string to decrypt:\n  > ");
                encrypted = Console.ReadLine()?.Trim() ?? "";
            }

            if (string.IsNullOrWhiteSpace(encrypted))
            {
                Console.WriteLine("  ✗ Input cannot be empty.");
                return 1;
            }

            string decrypted = CredentialProtector.Decrypt(encrypted);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  ✓ Decrypted:");
            Console.ResetColor();
            Console.WriteLine($"  {decrypted}");

            return 0;
        }

        // ── SCRIPT command ───────────────────────────────────────────────────
        static int RunGenerateScript(string[] args)
        {
            Console.WriteLine("  Mode: Generate SQL Server Setup Script");
            Console.WriteLine("  ───────────────────────────────────────");
            Console.WriteLine();

            Console.WriteLine("  Enter employee details for the script (empty login to finish):");

            var employees = new List<(string LoginName, string Password, string AppRole)>();
            while (true)
            {
                Console.Write("\n    Login name: ");
                string loginName = Console.ReadLine()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(loginName)) break;

                Console.Write("    Password: ");
                string password = ReadPassword();

                Console.Write("    Role (Admin/Lawyer/Paralegal): ");
                string role = Console.ReadLine()?.Trim() ?? "Paralegal";

                employees.Add((loginName, password, role));
            }

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<SqlServerSetupHelper>();

            // Use a placeholder master connection for script generation
            var setup = new SqlServerSetupHelper("Server=.;Database=master;Integrated Security=True;", "LawCaseManagement", logger);
            string script = setup.GenerateSetupScript(employees);

            Console.WriteLine("\n  Generated SQL Script:");
            Console.WriteLine("  ─────────────────────");
            Console.WriteLine(script);

            // Save to file
            string scriptPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LawCaseManagement",
                $"setup_script_{DateTime.Now:yyyyMMdd_HHmmss}.sql");

            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
            File.WriteAllText(scriptPath, script);
            Console.WriteLine($"\n  Script saved to: {scriptPath}");

            return 0;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        static void PrintUsage()
        {
            Console.WriteLine("  Usage:");
            Console.WriteLine("    dotnet run -- setup     [masterConnectionString]     Set up SQL Server database, roles, and logins");
            Console.WriteLine("    dotnet run -- migrate   [sqliteConn] [sqlServerConn] Migrate data from SQLite to SQL Server");
            Console.WriteLine("    dotnet run -- encrypt   [connectionString]           Encrypt a connection string using DPAPI");
            Console.WriteLine("    dotnet run -- decrypt   [encryptedString]            Decrypt a DPAPI-encrypted string");
            Console.WriteLine("    dotnet run -- script    [masterConnectionString]     Generate SQL setup script for DBA review");
            Console.WriteLine("    dotnet run -- help                                   Show this help");
            Console.WriteLine();
        }

        static int PrintUsageAndReturn() { PrintUsage(); return 0; }
        static int PrintUnknownCommand(string cmd) { Console.WriteLine($"  ✗ Unknown command: {cmd}"); PrintUsage(); return 1; }

        /// <summary>
        /// Reads a password from the console, masking input with asterisks.
        /// </summary>
        static string ReadPassword()
        {
            var password = new System.Text.StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password.Length--;
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    password.Append(key.KeyChar);
                    Console.Write("*");
                }
            }
            return password.ToString();
        }
    }
}
