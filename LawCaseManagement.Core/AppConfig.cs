namespace LawCaseManagement.Core
{
    /// <summary>
    /// Strongly-typed configuration loaded from appsettings.json at startup.
    /// </summary>
    public class AppConfig
    {
        public DatabaseConfig Database { get; set; } = new();
        public DocumentsConfig Documents { get; set; } = new();
    }

    public class DatabaseConfig
    {
        /// <summary>"SqlServer" or "Sqlite"</summary>
        public string Provider { get; set; } = "Sqlite";

        public string ConnectionString { get; set; } = "Data Source=law_case_management.db";
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
}
