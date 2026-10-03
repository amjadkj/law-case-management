using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LawCaseManagement.Core
{
    // ─── Enums ────────────────────────────────────────────────────────────────

    public enum UserRole
    {
        Admin,
        Lawyer,
        Paralegal
    }

    public enum CaseStatus
    {
        Open,
        InProgress,
        Closed,
        Archived
    }

    public enum CaseTypeEnum
    {
        Civil,
        Criminal,
        Family,
        Corporate,
        Other
    }

    public enum TaskStatus
    {
        Pending,
        InProgress,
        Completed
    }

    // ─── Constants (string representations stored in DB) ──────────────────────

    public static class Roles
    {
        public const string Admin     = "Admin";
        public const string Lawyer    = "Lawyer";
        public const string Paralegal = "Paralegal";
    }

    public static class CaseStatuses
    {
        public const string Open       = "Open";
        public const string InProgress = "In Progress";
        public const string Closed     = "Closed";
        public const string Archived   = "Archived";
    }

    public static class CaseTypes
    {
        public const string Civil      = "Civil";
        public const string Criminal   = "Criminal";
        public const string Family     = "Family";
        public const string Corporate  = "Corporate";
        public const string Other      = "Other";
    }

    public static class TaskStatuses
    {
        public const string Pending    = "Pending";
        public const string InProgress = "In Progress";
        public const string Completed  = "Completed";
    }

    // ─── Entities ─────────────────────────────────────────────────────────────

    public class User
    {
        public int UserID { get; set; }

        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [MaxLength(100)]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Role { get; set; } = Roles.Lawyer; // Admin, Lawyer, Paralegal

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<Case> LawyerCases { get; set; } = new List<Case>();
        public virtual ICollection<Case> ParalegalCases { get; set; } = new List<Case>();
        public virtual ICollection<Task> AssignedTasks { get; set; } = new List<Task>();
        public virtual ICollection<Document> UploadedDocuments { get; set; } = new List<Document>();
        public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }

    public class Case
    {
        public int CaseID { get; set; }

        [MaxLength(50)]
        public string FileNumber { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(50)]
        public string CaseType { get; set; } = CaseTypes.Civil;

        [MaxLength(50)]
        public string Status { get; set; } = CaseStatuses.Open;

        public DateTime OpenDate { get; set; }
        public DateTime? CloseDate { get; set; }
        public int LawyerID { get; set; }
        public int ParalegalID { get; set; }

        [MaxLength(4000)]
        public string Notes { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual User Lawyer { get; set; } = null!;
        public virtual User Paralegal { get; set; } = null!;
        public virtual ICollection<CaseClient> CaseClients { get; set; } = new List<CaseClient>();
        public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    }

    public class Client
    {
        public int ClientID { get; set; }

        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<CaseClient> CaseClients { get; set; } = new List<CaseClient>();
    }

    public class CaseClient
    {
        public int CaseID { get; set; }
        public virtual Case Case { get; set; } = null!;

        public int ClientID { get; set; }
        public virtual Client Client { get; set; } = null!;
    }

    public class Task
    {
        public int TaskID { get; set; }
        public int CaseID { get; set; }

        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        public int AssignedToID { get; set; }
        public DateTime DueDate { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = TaskStatuses.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Case Case { get; set; } = null!;
        public virtual User AssignedTo { get; set; } = null!;
    }

    public class Document
    {
        public int DocumentID { get; set; }
        public int CaseID { get; set; }

        [MaxLength(500)]
        public string FileName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string FilePath { get; set; } = string.Empty;

        public int UploadedByID { get; set; }
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Case Case { get; set; } = null!;
        public virtual User UploadedBy { get; set; } = null!;
    }

    public class AuditLog
    {
        public int LogID { get; set; }
        public int? UserID { get; set; }

        [MaxLength(200)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Details { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual User? User { get; set; }
    }
}
