using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LawCaseManagement.Core
{
    public interface IAdminService
    {
        List<User> GetUsers();
        bool CreateUser(User u, string plainPassword);
        bool UpdateUser(User u, string? plainPassword = null);
        bool ToggleUserActive(int userId);
        List<AuditLog> GetAuditLogs(string? filterText = null);
    }

    public class AdminService : IAdminService
    {
        private readonly IDbContextFactory<CaseDbContext>? _contextFactory;
        private readonly IAuditService _auditService;
        private readonly ILogger<AdminService> _logger;

        public AdminService(
            IDbContextFactory<CaseDbContext>? contextFactory = null,
            IAuditService? auditService = null,
            ILogger<AdminService>? logger = null)
        {
            _contextFactory = contextFactory;
            _auditService = auditService ?? new AuditService(contextFactory);
            _logger = logger ?? NullLogger<AdminService>.Instance;
        }

        private CaseDbContext CreateDbContext() => _contextFactory != null ? _contextFactory.CreateDbContext() : new CaseDbContext();

        public List<User> GetUsers()
        {
            using var db = CreateDbContext();
            return db.Users.OrderBy(u => u.FullName).ToList();
        }

        public bool CreateUser(User u, string plainPassword)
        {
            if (string.IsNullOrWhiteSpace(plainPassword))
                return false;

            try
            {
                using var db = CreateDbContext();
                // Check if username already exists
                if (db.Users.Any(user => user.Username.ToLower() == u.Username.ToLower()))
                    return false;

                u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
                u.CreatedAt = DateTime.UtcNow;

                db.Users.Add(u);
                db.SaveChanges();

                _auditService.Log(AuthService.CurrentUser?.UserID, "Create User", $"Created user account: {u.Username} (Role: {u.Role})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user {Username}", u.Username);
                return false;
            }
        }

        public bool UpdateUser(User u, string? plainPassword = null)
        {
            try
            {
                using var db = CreateDbContext();
                var existingUser = db.Users.FirstOrDefault(user => user.UserID == u.UserID);
                if (existingUser == null) return false;

                // Verify username uniqueness if changed
                if (existingUser.Username.ToLower() != u.Username.ToLower() &&
                    db.Users.Any(user => user.Username.ToLower() == u.Username.ToLower()))
                {
                    return false;
                }

                existingUser.FullName = u.FullName;
                existingUser.Username = u.Username;
                existingUser.Role = u.Role;
                existingUser.IsActive = u.IsActive;

                if (!string.IsNullOrWhiteSpace(plainPassword))
                {
                    existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
                }

                db.SaveChanges();

                _auditService.Log(AuthService.CurrentUser?.UserID, "Update User", $"Updated user account: {u.Username}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user ID {UserID}", u.UserID);
                return false;
            }
        }

        public bool ToggleUserActive(int userId)
        {
            try
            {
                string username = "";
                bool newStatus = false;
                using var db = CreateDbContext();
                var user = db.Users.FirstOrDefault(u => u.UserID == userId);
                if (user == null) return false;

                // Cannot deactivate the currently logged-in user
                if (AuthService.CurrentUser != null && AuthService.CurrentUser.UserID == userId)
                    return false;

                user.IsActive = !user.IsActive;
                newStatus = user.IsActive;
                username = user.Username;

                db.SaveChanges();

                _auditService.Log(AuthService.CurrentUser?.UserID, "Toggle User Active", $"Changed status for {username} to IsActive={newStatus}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user status for user ID {UserID}", userId);
                return false;
            }
        }

        public List<AuditLog> GetAuditLogs(string? filterText = null)
        {
            using var db = CreateDbContext();
            var query = db.AuditLogs
                .Include(al => al.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filterText))
            {
                filterText = filterText.ToLower();
                query = query.Where(al => al.Action.ToLower().Contains(filterText) ||
                                         al.Details.ToLower().Contains(filterText) ||
                                         (al.User != null && al.User.FullName.ToLower().Contains(filterText)));
            }

            return query.OrderByDescending(al => al.Timestamp).ToList();
        }
    }
}
