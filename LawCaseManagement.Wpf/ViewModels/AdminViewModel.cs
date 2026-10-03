using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using LawCaseManagement.Core;

namespace LawCaseManagement.Wpf.ViewModels
{
    public class AdminViewModel : ViewModelBase
    {
        private readonly IAdminService _adminService;
        private User? _selectedUser;
        private string _auditLogSearchQuery = string.Empty;

        // Form Fields
        private string _formFullName = string.Empty;
        private string _formUsername = string.Empty;
        private string _formPassword = string.Empty;
        private string _formRole = Roles.Lawyer;
        private bool _formIsActive = true;

        public ObservableCollection<User> UsersList { get; } = new ObservableCollection<User>();
        public ObservableCollection<AuditLog> AuditLogsList { get; } = new ObservableCollection<AuditLog>();

        public User? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    OnPropertyChanged(nameof(HasSelectedUser));
                    LoadSelectedUserDetails();
                }
            }
        }

        public bool HasSelectedUser => SelectedUser != null;

        public string AuditLogSearchQuery
        {
            get => _auditLogSearchQuery;
            set => SetProperty(ref _auditLogSearchQuery, value);
        }

        public string FormFullName { get => _formFullName; set => SetProperty(ref _formFullName, value); }
        public string FormUsername { get => _formUsername; set => SetProperty(ref _formUsername, value); }
        public string FormPassword { get => _formPassword; set => SetProperty(ref _formPassword, value); }
        public string FormRole { get => _formRole; set => SetProperty(ref _formRole, value); }
        public bool FormIsActive { get => _formIsActive; set => SetProperty(ref _formIsActive, value); }

        public ICommand CreateUserCommand { get; }
        public ICommand UpdateUserCommand { get; }
        public ICommand ToggleUserActiveCommand { get; }
        public ICommand ClearFormCommand { get; }
        public ICommand SearchLogsCommand { get; }
        public ICommand RefreshDataCommand { get; }

        public AdminViewModel(
            IAdminService? adminService = null,
            IDbContextFactory<CaseDbContext>? contextFactory = null)
        {
            _adminService = adminService ?? new AdminService(contextFactory);
            CreateUserCommand = new RelayCommand(ExecuteCreateUser);
            UpdateUserCommand = new RelayCommand(ExecuteUpdateUser);
            ToggleUserActiveCommand = new RelayCommand(ExecuteToggleUserActive);
            ClearFormCommand = new RelayCommand(ClearForm);
            SearchLogsCommand = new RelayCommand(LoadAuditLogs);
            RefreshDataCommand = new RelayCommand(LoadData);

            LoadData();
        }

        public void LoadData()
        {
            LoadUsers();
            LoadAuditLogs();
        }

        public void LoadUsers()
        {
            var list = _adminService.GetUsers();
            UsersList.Clear();
            foreach (var u in list)
            {
                UsersList.Add(u);
            }
        }

        public void LoadAuditLogs()
        {
            var logs = _adminService.GetAuditLogs(AuditLogSearchQuery);
            AuditLogsList.Clear();
            foreach (var log in logs)
            {
                AuditLogsList.Add(log);
            }
        }

        private void LoadSelectedUserDetails()
        {
            if (SelectedUser == null)
            {
                ClearForm();
                return;
            }

            FormFullName = SelectedUser.FullName;
            FormUsername = SelectedUser.Username;
            FormPassword = string.Empty; // Don't bind hash
            FormRole = SelectedUser.Role;
            FormIsActive = SelectedUser.IsActive;
        }

        private void ClearForm()
        {
            FormFullName = string.Empty;
            FormUsername = string.Empty;
            FormPassword = string.Empty;
            FormRole = Roles.Lawyer;
            FormIsActive = true;
            SelectedUser = null;
        }

        private void ExecuteCreateUser()
        {
            if (string.IsNullOrWhiteSpace(FormFullName) || string.IsNullOrWhiteSpace(FormUsername) ||
                string.IsNullOrWhiteSpace(FormPassword))
            {
                MessageBox.Show("Please fill out Name, Username, and Password fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var u = new User
            {
                FullName = FormFullName,
                Username = FormUsername,
                Role = FormRole,
                IsActive = FormIsActive
            };

            bool success = _adminService.CreateUser(u, FormPassword);
            if (success)
            {
                MessageBox.Show("User account created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadUsers();
            }
            else
            {
                MessageBox.Show("Failed to create user. Ensure username is unique.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteUpdateUser()
        {
            if (SelectedUser == null) return;

            if (string.IsNullOrWhiteSpace(FormFullName) || string.IsNullOrWhiteSpace(FormUsername))
            {
                MessageBox.Show("Please fill out Name and Username.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var u = new User
            {
                UserID = SelectedUser.UserID,
                FullName = FormFullName,
                Username = FormUsername,
                Role = FormRole,
                IsActive = FormIsActive
            };

            bool success = _adminService.UpdateUser(u, FormPassword);
            if (success)
            {
                MessageBox.Show("User account updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadUsers();
            }
            else
            {
                MessageBox.Show("Failed to update user.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteToggleUserActive(object? parameter)
        {
            if (parameter is User u)
            {
                bool success = _adminService.ToggleUserActive(u.UserID);
                if (success)
                {
                    LoadUsers();
                }
                else
                {
                    MessageBox.Show("Cannot toggle status of current active session user.", "Action Prevented", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}
