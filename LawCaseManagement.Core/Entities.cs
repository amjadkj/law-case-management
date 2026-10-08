using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LawCaseManagement.Core
{
    // ─── Sync Infrastructure ─────────────────────────────────────────────────

    /// <summary>
    /// Marker interface for entities that participate in offline sync.
    /// Provides RowVersion for concurrency detection and audit trail for sync.
    /// </summary>
    public interface ISyncableEntity
    {
        /// <summary>
        /// SQL Server: mapped to rowversion (auto-incremented binary timestamp).
        /// SQLite: manually managed as an incrementing integer stored as byte[].
        /// Used for optimistic concurrency and conflict detection during sync.
        /// </summary>
        byte[]? RowVersion { get; set; }

        /// <summary>UTC timestamp of the last modification.</summary>
        DateTime LastModifiedUtc { get; set; }

        /// <summary>UserID of the person who last modified this record. Null for system/seed operations.</summary>
        int? LastModifiedByUserId { get; set; }
    }

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

    public class Case : ISyncableEntity
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

        // ── Sync / Concurrency columns ───────────────────────────────────
        [Timestamp]
        public byte[]? RowVersion { get; set; }
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
        public int? LastModifiedByUserId { get; set; }

        // Navigation properties
        public virtual User Lawyer { get; set; } = null!;
        public virtual User Paralegal { get; set; } = null!;
        public virtual ICollection<CaseClient> CaseClients { get; set; } = new List<CaseClient>();
        public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    }

    public class Client : ISyncableEntity
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

        // ── Sync / Concurrency columns ───────────────────────────────────
        [Timestamp]
        public byte[]? RowVersion { get; set; }
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
        public int? LastModifiedByUserId { get; set; }

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

    public class Task : ISyncableEntity
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

        // ── Sync / Concurrency columns ───────────────────────────────────
        [Timestamp]
        public byte[]? RowVersion { get; set; }
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
        public int? LastModifiedByUserId { get; set; }

        // Navigation properties
        public virtual Case Case { get; set; } = null!;
        public virtual User AssignedTo { get; set; } = null!;
    }

    public class Document : ISyncableEntity
    {
        public int DocumentID { get; set; }
        public int CaseID { get; set; }

        [MaxLength(500)]
        public string FileName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string FilePath { get; set; } = string.Empty;

        public int UploadedByID { get; set; }
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        // ── Sync / Concurrency columns ───────────────────────────────────
        [Timestamp]
        public byte[]? RowVersion { get; set; }
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
        public int? LastModifiedByUserId { get; set; }

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

    // ─── Sync Change Log (Offline Change Queue) ──────────────────────────────

    /// <summary>
    /// Tracks offline changes made to the local SQLite cache.
    /// Used by the sync service (Phase 3) to push queued changes to SQL Server.
    /// </summary>
    public class SyncChangeLog
    {
        public int SyncChangeLogID { get; set; }

        /// <summary>Entity type name (e.g., "Case", "Client", "Task", "Document")</summary>
        [MaxLength(100)]
        public string EntityType { get; set; } = string.Empty;

        /// <summary>Primary key of the changed record</summary>
        public int RecordId { get; set; }

        /// <summary>Operation type: "Create", "Update", "Delete"</summary>
        [MaxLength(20)]
        public string Operation { get; set; } = string.Empty;

        /// <summary>JSON-serialized payload of the changed entity (for Create/Update)</summary>
        public string? Payload { get; set; }

        /// <summary>When the change was made locally</summary>
        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>UserID who made the change</summary>
        public int? ChangedByUserId { get; set; }

        /// <summary>Whether this change has been synced to the server</summary>
        public bool IsSynced { get; set; } = false;

        /// <summary>When the change was successfully synced (null if pending)</summary>
        public DateTime? SyncedAtUtc { get; set; }

        /// <summary>Error message if sync failed</summary>
        [MaxLength(2000)]
        public string? SyncError { get; set; }
    }

    // ─── Sync Conflict Record ────────────────────────────────────────────────

    /// <summary>
    /// Records a conflict detected during sync when the server version has
    /// diverged from the local cached version. Admins review and resolve.
    /// </summary>
    public class SyncConflict
    {
        public int SyncConflictID { get; set; }

        [MaxLength(100)]
        public string EntityType { get; set; } = string.Empty;

        public int RecordId { get; set; }

        /// <summary>JSON-serialized local (offline) version of the record</summary>
        public string? LocalPayload { get; set; }

        /// <summary>JSON-serialized server version of the record</summary>
        public string? ServerPayload { get; set; }

        /// <summary>When the conflict was detected</summary>
        public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Resolution: null = unresolved, "KeepServer", "KeepLocal", "Merged"</summary>
        [MaxLength(50)]
        public string? Resolution { get; set; }

        /// <summary>UserID of the admin who resolved the conflict</summary>
        public int? ResolvedByUserId { get; set; }

        /// <summary>When the conflict was resolved</summary>
        public DateTime? ResolvedAtUtc { get; set; }

        /// <summary>JSON payload of the merged result (if Resolution = "Merged")</summary>
        public string? MergedPayload { get; set; }
    }
}
