using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using LawCaseManagement.Core;
using CoreTask = LawCaseManagement.Core.Task;

namespace LawCaseManagement.Wpf.ViewModels
{
    public class CasesViewModel : ViewModelBase
    {
        private readonly ICaseService _caseService;
        private readonly IClientService _clientService;
        private readonly ITaskService _taskService;
        private readonly IDocumentService _documentService;
        private readonly IDbContextFactory<CaseDbContext>? _contextFactory;

        private Case? _selectedCase;
        private string _searchQuery = string.Empty;
        private string _statusFilter = "All";
        private string _typeFilter = "All";

        // For Creation / Editing Form
        private string _formFileNumber = string.Empty;
        private string _formTitle = string.Empty;
        private string _formCaseType = CaseTypes.Civil;
        private string _formStatus = CaseStatuses.Open;
        private string _formNotes = string.Empty;
        private User? _formSelectedLawyer;
        private User? _formSelectedParalegal;
        private Client? _formSelectedClientToLink;
        private string _formNewTaskTitle = string.Empty;
        private User? _formNewTaskAssignedTo;
        private DateTime _formNewTaskDueDate = DateTime.Today.AddDays(7);

        private DateTime _formOpenDate = DateTime.Today;
        private DateTime? _formCloseDate;

        public bool IsAdminOrLawyer => AuthService.CurrentUser?.Role == Roles.Admin || AuthService.CurrentUser?.Role == Roles.Lawyer;

        public ObservableCollection<Case> CasesList { get; } = new ObservableCollection<Case>();
        public ObservableCollection<User> LawyersList { get; } = new ObservableCollection<User>();
        public ObservableCollection<User> ParalegalsList { get; } = new ObservableCollection<User>();
        public ObservableCollection<User> AllStaffList { get; } = new ObservableCollection<User>();
        public ObservableCollection<Client> AllClientsList { get; } = new ObservableCollection<Client>();
        public ObservableCollection<Client> LinkedClientsForForm { get; } = new ObservableCollection<Client>();

        public Case? SelectedCase
        {
            get => _selectedCase;
            set
            {
                if (SetProperty(ref _selectedCase, value))
                {
                    OnPropertyChanged(nameof(HasSelectedCase));
                    LoadSelectedCaseDetails();
                }
            }
        }

        public bool HasSelectedCase => SelectedCase != null;

        public string SearchQuery
        {
            get => _searchQuery;
            set => SetProperty(ref _searchQuery, value);
        }

        public string StatusFilter
        {
            get => _statusFilter;
            set => SetProperty(ref _statusFilter, value);
        }

        public string TypeFilter
        {
            get => _typeFilter;
            set => SetProperty(ref _typeFilter, value);
        }

        // Form bindings
        public string FormFileNumber { get => _formFileNumber; set => SetProperty(ref _formFileNumber, value); }
        public string FormTitle { get => _formTitle; set => SetProperty(ref _formTitle, value); }
        public string FormCaseType { get => _formCaseType; set => SetProperty(ref _formCaseType, value); }
        public string FormStatus { get => _formStatus; set => SetProperty(ref _formStatus, value); }
        public string FormNotes { get => _formNotes; set => SetProperty(ref _formNotes, value); }
        public DateTime FormOpenDate { get => _formOpenDate; set => SetProperty(ref _formOpenDate, value); }
        public DateTime? FormCloseDate { get => _formCloseDate; set => SetProperty(ref _formCloseDate, value); }
        public User? FormSelectedLawyer { get => _formSelectedLawyer; set => SetProperty(ref _formSelectedLawyer, value); }
        public User? FormSelectedParalegal { get => _formSelectedParalegal; set => SetProperty(ref _formSelectedParalegal, value); }
        public Client? FormSelectedClientToLink { get => _formSelectedClientToLink; set => SetProperty(ref _formSelectedClientToLink, value); }

        // Task Form bindings
        public string FormNewTaskTitle { get => _formNewTaskTitle; set => SetProperty(ref _formNewTaskTitle, value); }
        public User? FormNewTaskAssignedTo { get => _formNewTaskAssignedTo; set => SetProperty(ref _formNewTaskAssignedTo, value); }
        public DateTime FormNewTaskDueDate { get => _formNewTaskDueDate; set => SetProperty(ref _formNewTaskDueDate, value); }

        public ICommand SearchCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand AutoGenerateFileNumberCommand { get; }
        public ICommand CreateCaseCommand { get; }
        public ICommand UpdateCaseCommand { get; }
        public ICommand AddClientToLinkFormCommand { get; }
        public ICommand RemoveClientFromLinkFormCommand { get; }
        
        // Document/Task actions for selected case
        public ICommand UploadDocumentCommand { get; }
        public ICommand OpenDocumentCommand { get; }
        public ICommand DeleteDocumentCommand { get; }
        public ICommand CreateTaskCommand { get; }
        public ICommand ToggleTaskStatusCommand { get; }

        public CasesViewModel(
            ICaseService? caseService = null,
            IClientService? clientService = null,
            ITaskService? taskService = null,
            IDocumentService? documentService = null,
            IDbContextFactory<CaseDbContext>? contextFactory = null)
        {
            _contextFactory = contextFactory;
            _caseService = caseService ?? new CaseService(contextFactory);
            _clientService = clientService ?? new ClientService(contextFactory);
            _taskService = taskService ?? new TaskService(contextFactory);
            _documentService = documentService ?? new DocumentService(contextFactory);

            SearchCommand = new RelayCommand(LoadCases);
            ClearFiltersCommand = new RelayCommand(ResetFilters);
            AutoGenerateFileNumberCommand = new RelayCommand(ExecuteAutoGenerateFileNumber);
            AddClientToLinkFormCommand = new RelayCommand(AddClientToForm);
            RemoveClientFromLinkFormCommand = new RelayCommand(RemoveClientFromForm);
            CreateCaseCommand = new RelayCommand(ExecuteCreateCase);
            UpdateCaseCommand = new RelayCommand(ExecuteUpdateCase);

            UploadDocumentCommand = new RelayCommand(ExecuteUploadDocument);
            OpenDocumentCommand = new RelayCommand(ExecuteOpenDocument);
            DeleteDocumentCommand = new RelayCommand(ExecuteDeleteDocument);
            CreateTaskCommand = new RelayCommand(ExecuteCreateTask);
            ToggleTaskStatusCommand = new RelayCommand(ExecuteToggleTaskStatus);

            LoadStaticListData();
            LoadCases();
        }

        private CaseDbContext CreateDbContext() => _contextFactory != null ? _contextFactory.CreateDbContext() : new CaseDbContext();

        public void LoadStaticListData()
        {
            using var db = CreateDbContext();
            LawyersList.Clear();
            foreach (var user in db.Users.Where(u => u.Role == Roles.Lawyer && u.IsActive).ToList())
            {
                LawyersList.Add(user);
            }

            ParalegalsList.Clear();
            foreach (var user in db.Users.Where(u => u.Role == Roles.Paralegal && u.IsActive).ToList())
            {
                ParalegalsList.Add(user);
            }

            AllStaffList.Clear();
            foreach (var user in db.Users.Where(u => u.IsActive && (u.Role == Roles.Lawyer || u.Role == Roles.Paralegal)).OrderBy(u => u.FullName).ToList())
            {
                AllStaffList.Add(user);
            }

            AllClientsList.Clear();
            foreach (var client in db.Clients.OrderBy(c => c.FullName).ToList())
            {
                AllClientsList.Add(client);
            }
        }

        private void ExecuteAutoGenerateFileNumber()
        {
            FormFileNumber = _caseService.GenerateNextFileNumber();
        }

        public void LoadCases()
        {
            var list = _caseService.GetCases(SearchQuery, StatusFilter, TypeFilter);
            CasesList.Clear();
            foreach (var c in list)
            {
                CasesList.Add(c);
            }
        }

        private void ResetFilters()
        {
            SearchQuery = string.Empty;
            StatusFilter = "All";
            TypeFilter = "All";
            LoadCases();
        }

        private void AddClientToForm()
        {
            if (FormSelectedClientToLink != null && !LinkedClientsForForm.Any(c => c.ClientID == FormSelectedClientToLink.ClientID))
            {
                LinkedClientsForForm.Add(FormSelectedClientToLink);
            }
        }

        private void RemoveClientFromForm(object? parameter)
        {
            if (parameter is Client client)
            {
                LinkedClientsForForm.Remove(client);
            }
        }

        private void ExecuteOpenDocument(object? parameter)
        {
            if (parameter is Document doc)
            {
                if (!DocumentService.OpenDocument(doc.FilePath))
                {
                    MessageBox.Show("Unable to open file. Please check if the file exists or if an application is configured to open it.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteCreateCase()
        {
            if (string.IsNullOrWhiteSpace(FormFileNumber) || string.IsNullOrWhiteSpace(FormTitle) ||
                FormSelectedLawyer == null || FormSelectedParalegal == null)
            {
                MessageBox.Show("Please fill out all required fields: File Number, Title, Lawyer, and Paralegal.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newCase = new Case
            {
                FileNumber = FormFileNumber,
                Title = FormTitle,
                CaseType = FormCaseType,
                Status = FormStatus,
                Notes = FormNotes,
                LawyerID = FormSelectedLawyer.UserID,
                ParalegalID = FormSelectedParalegal.UserID,
                OpenDate = DateTime.Today
            };

            var clientIds = LinkedClientsForForm.Select(c => c.ClientID).ToList();
            bool success = _caseService.CreateCase(newCase, clientIds);

            if (success)
            {
                MessageBox.Show("Case file created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadCases();
            }
            else
            {
                MessageBox.Show("Failed to create case. Ensure the File Number is unique and permissions are sufficient.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteUpdateCase()
        {
            if (SelectedCase == null) return;

            if (string.IsNullOrWhiteSpace(FormFileNumber) || string.IsNullOrWhiteSpace(FormTitle) ||
                FormSelectedLawyer == null || FormSelectedParalegal == null)
            {
                MessageBox.Show("Please fill out all required fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var updated = new Case
            {
                CaseID = SelectedCase.CaseID,
                FileNumber = FormFileNumber,
                Title = FormTitle,
                CaseType = FormCaseType,
                Status = FormStatus,
                Notes = FormNotes,
                LawyerID = FormSelectedLawyer.UserID,
                ParalegalID = FormSelectedParalegal.UserID
            };

            var clientIds = LinkedClientsForForm.Select(c => c.ClientID).ToList();
            bool success = _caseService.UpdateCase(updated, clientIds);

            if (success)
            {
                MessageBox.Show("Case file updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadCases();
                SelectedCase = _caseService.GetCaseById(SelectedCase.CaseID); // reload details
            }
            else
            {
                MessageBox.Show("Failed to update case.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSelectedCaseDetails()
        {
            if (SelectedCase == null)
            {
                ClearForm();
                return;
            }

            var details = _caseService.GetCaseById(SelectedCase.CaseID);
            if (details == null)
            {
                ClearForm();
                return;
            }

            FormFileNumber = details.FileNumber;
            FormTitle = details.Title;
            FormCaseType = details.CaseType;
            FormStatus = details.Status;
            FormNotes = details.Notes;
            FormSelectedLawyer = LawyersList.FirstOrDefault(l => l.UserID == details.LawyerID);
            FormSelectedParalegal = ParalegalsList.FirstOrDefault(p => p.UserID == details.ParalegalID);

            LinkedClientsForForm.Clear();
            foreach (var cc in details.CaseClients)
            {
                LinkedClientsForForm.Add(cc.Client);
            }
        }

        private void ClearForm()
        {
            FormFileNumber = string.Empty;
            FormTitle = string.Empty;
            FormCaseType = CaseTypes.Civil;
            FormStatus = CaseStatuses.Open;
            FormNotes = string.Empty;
            FormSelectedLawyer = null;
            FormSelectedParalegal = null;
            LinkedClientsForForm.Clear();
        }

        private void ExecuteUploadDocument()
        {
            if (SelectedCase == null) return;
            if (AuthService.CurrentUser == null)
            {
                MessageBox.Show("No active user session.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var openFileDialog = new OpenFileDialog
            {
                Title = "Select File to Upload",
                Filter = "All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string filePath = openFileDialog.FileName;
                    _documentService.UploadDocument(SelectedCase.CaseID, filePath, AuthService.CurrentUser.UserID);
                    MessageBox.Show("Document uploaded successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadSelectedCaseDetails(); // Reload document list
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Upload Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteDeleteDocument(object? parameter)
        {
            if (parameter is Document doc && AuthService.CurrentUser != null)
            {
                var result = MessageBox.Show($"Are you sure you want to remove document \"{doc.FileName}\"?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    bool success = _documentService.DeleteDocument(doc.DocumentID, AuthService.CurrentUser.UserID);
                    if (success)
                    {
                        LoadSelectedCaseDetails();
                    }
                    else
                    {
                        MessageBox.Show("Failed to delete document.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ExecuteCreateTask()
        {
            if (SelectedCase == null) return;

            if (string.IsNullOrWhiteSpace(FormNewTaskTitle) || FormNewTaskAssignedTo == null)
            {
                MessageBox.Show("Please enter a title and assign the task to a staff member.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newTask = new CoreTask
            {
                CaseID = SelectedCase.CaseID,
                Title = FormNewTaskTitle,
                AssignedToID = FormNewTaskAssignedTo.UserID,
                DueDate = FormNewTaskDueDate,
                Status = TaskStatuses.Pending
            };

            bool success = _taskService.CreateTask(newTask);
            if (success)
            {
                FormNewTaskTitle = string.Empty;
                FormNewTaskAssignedTo = null;
                FormNewTaskDueDate = DateTime.Today.AddDays(7);
                LoadSelectedCaseDetails(); // Reload task list
            }
            else
            {
                MessageBox.Show("Failed to create task.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteToggleTaskStatus(object? parameter)
        {
            if (parameter is CoreTask t)
            {
                if (t.Status == TaskStatuses.Pending) t.Status = TaskStatuses.InProgress;
                else if (t.Status == TaskStatuses.InProgress) t.Status = TaskStatuses.Completed;
                else t.Status = TaskStatuses.Pending;

                bool success = _taskService.UpdateTask(t);
                if (success)
                {
                    LoadSelectedCaseDetails();
                }
            }
        }
    }
}
