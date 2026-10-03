using System;
using Microsoft.EntityFrameworkCore;

namespace LawCaseManagement.Core
{
    public class CaseDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Case> Cases { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<CaseClient> CaseClients { get; set; }
        public DbSet<Task> Tasks { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        // Parameterless constructor kept for EF tooling (migrations)
        public CaseDbContext()
        {
        }

        public CaseDbContext(DbContextOptions<CaseDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Fallback configuration used only by EF tooling (dotnet ef migrations).
        /// At runtime the DI container always provides DbContextOptions.
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Dev fallback: SQLite in the project root
                optionsBuilder.UseSqlite("Data Source=law_case_management.db");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasKey(u => u.UserID);
            modelBuilder.Entity<Case>().HasKey(c => c.CaseID);
            modelBuilder.Entity<Client>().HasKey(cl => cl.ClientID);
            modelBuilder.Entity<Task>().HasKey(t => t.TaskID);
            modelBuilder.Entity<Document>().HasKey(d => d.DocumentID);
            modelBuilder.Entity<AuditLog>().HasKey(al => al.LogID);

            // ── Unique constraint on FileNumber ───────────────────────────────
            modelBuilder.Entity<Case>()
                .HasIndex(c => c.FileNumber)
                .IsUnique();

            // ── CaseClient Junction Table ─────────────────────────────────────
            modelBuilder.Entity<CaseClient>()
                .HasKey(cc => new { cc.CaseID, cc.ClientID });

            modelBuilder.Entity<CaseClient>()
                .HasOne(cc => cc.Case)
                .WithMany(c => c.CaseClients)
                .HasForeignKey(cc => cc.CaseID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CaseClient>()
                .HasOne(cc => cc.Client)
                .WithMany(cl => cl.CaseClients)
                .HasForeignKey(cc => cc.ClientID)
                .OnDelete(DeleteBehavior.Cascade);

            // ── User – Cases (Lawyer / Paralegal) ─────────────────────────────
            modelBuilder.Entity<Case>()
                .HasOne(c => c.Lawyer)
                .WithMany(u => u.LawyerCases)
                .HasForeignKey(c => c.LawyerID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Case>()
                .HasOne(c => c.Paralegal)
                .WithMany(u => u.ParalegalCases)
                .HasForeignKey(c => c.ParalegalID)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Tasks ─────────────────────────────────────────────────────────
            modelBuilder.Entity<Task>()
                .HasOne(t => t.Case)
                .WithMany(c => c.Tasks)
                .HasForeignKey(t => t.CaseID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Task>()
                .HasOne(t => t.AssignedTo)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(t => t.AssignedToID)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Documents ─────────────────────────────────────────────────────
            modelBuilder.Entity<Document>()
                .HasOne(d => d.Case)
                .WithMany(c => c.Documents)
                .HasForeignKey(d => d.CaseID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Document>()
                .HasOne(d => d.UploadedBy)
                .WithMany(u => u.UploadedDocuments)
                .HasForeignKey(d => d.UploadedByID)
                .OnDelete(DeleteBehavior.Restrict);

            // ── AuditLog ──────────────────────────────────────────────────────
            modelBuilder.Entity<AuditLog>()
                .HasOne(al => al.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(al => al.UserID)
                .OnDelete(DeleteBehavior.SetNull);

            base.OnModelCreating(modelBuilder);
        }
    }
}
