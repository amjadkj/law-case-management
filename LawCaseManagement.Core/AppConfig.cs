namespace LawCaseManagement.Core
{
    /// <summary>
    /// Strongly-typed configuration loaded from appsettings.json at startup.
    /// </summary>
    public class AppConfig
    {
        public DatabaseConfig Database { get; set; } = new();
        public DocumentsConfig Documents { get; set; } = new();
        public SyncConfig Sync { get; set; } = new();
    }

    public class DatabaseConfig
    {
        /// <summary>"SqlServer" or "Sqlite"</summary>
        public string Provider { get; set; } = "Sqlite";

        public string ConnectionString { get; set; } = "Data Source=law_case_management.db";

        /// <summary>
        /// Connection string for the SQL Server primary database.
        /// Used when Provider is "SqlServer", or when syncing from SQLite cache to SQL Server.
        /// Format: "Server=192.168.x.x;Database=LawCaseManagement;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True"
        /// </summary>
        public string SqlServerConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Connection string for the local SQLite cache database.
        /// Used when operating in hybrid mode (offline fallback).
        /// </summary>
        public string SqliteCacheConnectionString { get; set; } = "Data Source=law_case_management_cache.db";
    }

    public class DocumentsConfig
    {
        /// <summary>
        /// Root folder where uploaded documents are stored.
        /// For production: a shared network path e.g. \\SERVER-PC\LawFirmDocuments
        /// For dev:        left empty, defaults to AppData\Local\LawCaseManagement\Documents
        /// </summary>
        public string SharedFolder { get; set; } = string.Empty;
    }

    /// <summary>
    /// Configuration for the offline sync service (Phase 3).
    /// Defined here so the schema is ready when sync is implemented.
    /// </summary>
    public class SyncConfig
    {
        /// <summary>Whether offline sync is enabled. Default false until Phase 3.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Interval in seconds between connectivity checks and sync attempts.</summary>
        public int SyncIntervalSeconds { get; set; } = 30;

        /// <summary>Maximum number of retries for a failed sync push before parking the change.</summary>
        public int MaxRetries { get; set; } = 3;
    }
}
