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
        private User? _selectedLawyerFilter;
        private User? _selectedParalegalFilter;

        // Modal states (§2.7)
        private bool _isCaseModalOpen;
        private bool _isFilterModalOpen;
        private bool _isEditMode;
        private bool _hasConflict;

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

        public bool IsAdmin => AuthService.CurrentUser?.Role == Roles.Admin;
        public bool IsAdminOrLawyer => AuthService.CurrentUser?.Role == Roles.Admin || AuthService.CurrentUser?.Role == Roles.Lawyer;

        public ObservableCollection<Case> CasesList { get; } = new ObservableCollection<Case>();
        public ObservableCollection<User> LawyersList { get; } = new ObservableCollection<User>();
        public ObservableCollection<User> ParalegalsList { get; } = new ObservableCollection<User>();
        public ObservableCollection<User> AllStaffList { get; } = new ObservableCollection<User>();
        public ObservableCollection<Client> AllClientsList { get; } = new ObservableCollection<Client>();
        public ObservableCollection<Client> LinkedClientsForForm { get; } = new ObservableCollection<Client>();
        public ObservableCollection<Document> CaseDocuments { get; } = new ObservableCollection<Document>();
        public ObservableCollection<CoreTask> CaseTasks { get; } = new ObservableCollection<CoreTask>();

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

        // Modals & Banners
        public bool IsCaseModalOpen
        {
            get => _isCaseModalOpen;
            set => SetProperty(ref _isCaseModalOpen, value);
        }

        public bool IsFilterModalOpen
        {
            get => _isFilterModalOpen;
            set => SetProperty(ref _isFilterModalOpen, value);
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                if (SetProperty(ref _isEditMode, value))
                {
                    OnPropertyChanged(nameof(ModalTitle));
                    OnPropertyChanged(nameof(SaveButtonText));
                }
            }
        }

        public string ModalTitle => IsEditMode ? $"Case Details: {FormFileNumber}" : "New Case";
        public string SaveButtonText => IsEditMode ? "Save Changes" : "Create Case";

        public bool HasConflict
        {
            get => _hasConflict;
            set
            {
                if (SetProperty(ref _hasConflict, value))
                {
                    OnPropertyChanged(nameof(ShowConflictBanner));
                }
            }
        }

        public bool ShowConflictBanner => HasConflict && IsAdmin;

        // Scoped Search & Filter (§2.5)
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    LoadCases();
                }
            }
        }

        public string StatusFilter
        {
            get => _statusFilter;
            set
            {
                if (SetProperty(ref _statusFilter, value))
                {
                    OnPropertyChanged(nameof(HasActiveFilters));
                }
            }
        }

        public string TypeFilter
        {
            get => _typeFilter;
            set
            {
                if (SetProperty(ref _typeFilter, value))
                {
                    OnPropertyChanged(nameof(HasActiveFilters));
                }
            }
        }

        public User? SelectedLawyerFilter
        {
            get => _selectedLawyerFilter;
            set
            {
                if (SetProperty(ref _selectedLawyerFilter, value))
                {
                    OnPropertyChanged(nameof(HasActiveFilters));
                }
            }
        }

        public User? SelectedParalegalFilter
        {
            get => _selectedParalegalFilter;
            set
            {
                if (SetProperty(ref _selectedParalegalFilter, value))
                {
                    OnPropertyChanged(nameof(HasActiveFilters));
                }
            }
        }

        public bool HasActiveFilters => 
            (StatusFilter != "All" && !string.IsNullOrEmpty(StatusFilter)) ||
            (TypeFilter != "All" && !string.IsNullOrEmpty(TypeFilter)) ||
            SelectedLawyerFilter != null ||
            SelectedParalegalFilter != null;

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

        // Commands
        public ICommand SearchCommand { get; }
        public ICommand ToggleFilterModalCommand { get; }
        public ICommand ApplyFiltersCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand OpenCreateModalCommand { get; }
        public ICommand CloseCaseModalCommand { get; }
        public ICommand SaveCaseCommand { get; }
        public ICommand AutoGenerateFileNumberCommand { get; }
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
            ToggleFilterModalCommand = new RelayCommand(() => IsFilterModalOpen = !IsFilterModalOpen);
            ApplyFiltersCommand = new RelayCommand(() => { IsFilterModalOpen = false; LoadCases(); });
            ClearFiltersCommand = new RelayCommand(ResetFilters);

            OpenCreateModalCommand = new RelayCommand(OpenCreateModal);
            CloseCaseModalCommand = new RelayCommand(() => IsCaseModalOpen = false);
            SaveCaseCommand = new RelayCommand(ExecuteSaveCase);

            AutoGenerateFileNumberCommand = new RelayCommand(ExecuteAutoGenerateFileNumber);
            AddClientToLinkFormCommand = new RelayCommand(AddClientToForm);
            RemoveClientFromLinkFormCommand = new RelayCommand(RemoveClientFromForm);

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
            foreach (var user in db.Users.Where(u => u.Role == Roles.Lawyer && u.IsActive).OrderBy(u => u.FullName).ToList())
            {
                LawyersList.Add(user);
            }

            ParalegalsList.Clear();
            foreach (var user in db.Users.Where(u => u.Role == Roles.Paralegal && u.IsActive).OrderBy(u => u.FullName).ToList())
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

        public void OpenCreateModal()
        {
            ClearForm();
            IsEditMode = false;
            HasConflict = false;
            SelectedCase = null;
            ExecuteAutoGenerateFileNumber();
            IsCaseModalOpen = true;
        }

        public void OpenEditModal(Case c)
        {
            SelectedCase = c;
            IsEditMode = true;
            IsCaseModalOpen = true;
        }

        private void ExecuteAutoGenerateFileNumber()
        {
            FormFileNumber = _caseService.GenerateNextFileNumber();
        }

        public void LoadCases()
        {
            var list = _caseService.GetCases(SearchQuery, StatusFilter, TypeFilter);
            
            // Apply additional in-memory or helper filters if lawyer/paralegal selected
            if (SelectedLawyerFilter != null)
            {
                list = list.Where(c => c.LawyerID == SelectedLawyerFilter.UserID).ToList();
            }
            if (SelectedParalegalFilter != null)
            {
                list = list.Where(c => c.ParalegalID == SelectedParalegalFilter.UserID).ToList();
            }

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
            SelectedLawyerFilter = null;
            SelectedParalegalFilter = null;
            IsFilterModalOpen = false;
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

        private void ExecuteSaveCase()
        {
            if (IsEditMode)
            {
                ExecuteUpdateCase();
            }
            else
            {
                ExecuteCreateCase();
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
                OpenDate = FormOpenDate
            };

            var clientIds = LinkedClientsForForm.Select(c => c.ClientID).ToList();
            bool success = _caseService.CreateCase(newCase, clientIds);

            if (success)
            {
                MessageBox.Show("Case file created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                IsCaseModalOpen = false;
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
                IsCaseModalOpen = false;
                LoadCases();
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
                CaseDocuments.Clear();
                CaseTasks.Clear();
                HasConflict = false;
                return;
            }

            var details = _caseService.GetCaseById(SelectedCase.CaseID);
            if (details == null)
            {
                ClearForm();
                CaseDocuments.Clear();
                CaseTasks.Clear();
                HasConflict = false;
                return;
            }

            FormFileNumber = details.FileNumber;
            FormTitle = details.Title;
            FormCaseType = details.CaseType;
            FormStatus = details.Status;
            FormNotes = details.Notes;
            FormOpenDate = details.OpenDate;
            FormCloseDate = details.CloseDate;
            FormSelectedLawyer = LawyersList.FirstOrDefault(l => l.UserID == details.LawyerID);
            FormSelectedParalegal = ParalegalsList.FirstOrDefault(p => p.UserID == details.ParalegalID);

            LinkedClientsForForm.Clear();
            foreach (var cc in details.CaseClients)
            {
                if (cc.Client != null)
                {
                    LinkedClientsForForm.Add(cc.Client);
                }
            }

            CaseDocuments.Clear();
            foreach (var doc in details.Documents)
            {
                CaseDocuments.Add(doc);
            }

            CaseTasks.Clear();
            foreach (var task in details.Tasks)
            {
                CaseTasks.Add(task);
            }

            // Check for conflict record in DB (§3.3)
            try
            {
                using var db = CreateDbContext();
                HasConflict = db.SyncConflicts.Any(sc => sc.EntityType == "Case" && sc.RecordId == details.CaseID && sc.Resolution == null);
            }
            catch
            {
                HasConflict = false;
            }
        }

        private void ClearForm()
        {
            FormFileNumber = string.Empty;
            FormTitle = string.Empty;
            FormCaseType = CaseTypes.Civil;
            FormStatus = CaseStatuses.Open;
            FormNotes = string.Empty;
            FormOpenDate = DateTime.Today;
            FormCloseDate = null;
            FormSelectedLawyer = null;
            FormSelectedParalegal = null;
            LinkedClientsForForm.Clear();
            CaseDocuments.Clear();
            CaseTasks.Clear();
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
