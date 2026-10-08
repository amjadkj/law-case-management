using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using LawCaseManagement.Core;
using CoreTask = LawCaseManagement.Core.Task;

namespace LawCaseManagement.Wpf.ViewModels
{
    public class TasksViewModel : ViewModelBase
    {
        private readonly ITaskService _taskService;
        private readonly ICaseService _caseService;
        private readonly IDbContextFactory<CaseDbContext>? _contextFactory;

        private string _searchQuery = string.Empty;
        private string _statusFilter = "All";
        private User? _selectedStaffFilter;
        private Case? _selectedCaseFilter;
        private CoreTask? _selectedTask;

        // Modal states (§2.7)
        private bool _isTaskModalOpen;
        private bool _isFilterModalOpen;
        private bool _isEditMode;

        // Form Fields
        private string _formTitle = string.Empty;
        private Case? _formSelectedCase;
        private User? _formAssignedTo;
        private string _formStatus = TaskStatuses.Pending;
        private DateTime _formDueDate = DateTime.Today.AddDays(7);

        public ObservableCollection<CoreTask> TasksList { get; } = new ObservableCollection<CoreTask>();
        public ObservableCollection<User> StaffList { get; } = new ObservableCollection<User>();
        public ObservableCollection<Case> CasesList { get; } = new ObservableCollection<Case>();

        public CoreTask? SelectedTask
        {
            get => _selectedTask;
            set => SetProperty(ref _selectedTask, value);
        }

        // Modals
        public bool IsTaskModalOpen
        {
            get => _isTaskModalOpen;
            set => SetProperty(ref _isTaskModalOpen, value);
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

        public string ModalTitle => IsEditMode ? $"Task Details: {FormTitle}" : "New Task";
        public string SaveButtonText => IsEditMode ? "Save Changes" : "Create Task";

        // Scoped Search & Filter (§2.5)
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    LoadTasks();
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

        public User? SelectedStaffFilter
        {
            get => _selectedStaffFilter;
            set
            {
                if (SetProperty(ref _selectedStaffFilter, value))
                {
                    OnPropertyChanged(nameof(HasActiveFilters));
                }
            }
        }

        public Case? SelectedCaseFilter
        {
            get => _selectedCaseFilter;
            set
            {
                if (SetProperty(ref _selectedCaseFilter, value))
                {
                    OnPropertyChanged(nameof(HasActiveFilters));
                }
            }
        }

        public bool HasActiveFilters => 
            (StatusFilter != "All" && !string.IsNullOrEmpty(StatusFilter)) ||
            SelectedStaffFilter != null ||
            SelectedCaseFilter != null;

        // Form Bindings
        public string FormTitle { get => _formTitle; set => SetProperty(ref _formTitle, value); }
        public Case? FormSelectedCase { get => _formSelectedCase; set => SetProperty(ref _formSelectedCase, value); }
        public User? FormAssignedTo { get => _formAssignedTo; set => SetProperty(ref _formAssignedTo, value); }
        public string FormStatus { get => _formStatus; set => SetProperty(ref _formStatus, value); }
        public DateTime FormDueDate { get => _formDueDate; set => SetProperty(ref _formDueDate, value); }

        // Commands
        public ICommand SearchCommand { get; }
        public ICommand ToggleFilterModalCommand { get; }
        public ICommand ApplyFiltersCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand OpenCreateModalCommand { get; }
        public ICommand CloseTaskModalCommand { get; }
        public ICommand SaveTaskCommand { get; }
        public ICommand CycleStatusCommand { get; }
        public ICommand DeleteTaskCommand { get; }

        public TasksViewModel(
            ITaskService? taskService = null,
            ICaseService? caseService = null,
            IDbContextFactory<CaseDbContext>? contextFactory = null)
        {
            _contextFactory = contextFactory;
            _taskService = taskService ?? new TaskService(contextFactory);
            _caseService = caseService ?? new CaseService(contextFactory);

            SearchCommand = new RelayCommand(LoadTasks);
            ToggleFilterModalCommand = new RelayCommand(() => IsFilterModalOpen = !IsFilterModalOpen);
            ApplyFiltersCommand = new RelayCommand(() => { IsFilterModalOpen = false; LoadTasks(); });
            ClearFiltersCommand = new RelayCommand(ResetFilters);

            OpenCreateModalCommand = new RelayCommand(OpenCreateModal);
            CloseTaskModalCommand = new RelayCommand(() => IsTaskModalOpen = false);
            SaveTaskCommand = new RelayCommand(ExecuteSaveTask);

            CycleStatusCommand = new RelayCommand(ExecuteCycleStatus);
            DeleteTaskCommand = new RelayCommand(ExecuteDeleteTask);

            LoadStaticData();
            LoadTasks();
        }

        private CaseDbContext CreateDbContext() => _contextFactory != null ? _contextFactory.CreateDbContext() : new CaseDbContext();

        public void LoadStaticData()
        {
            using var db = CreateDbContext();
            StaffList.Clear();
            var activeStaff = db.Users.Where(u => u.IsActive && u.Role != Roles.Admin).OrderBy(u => u.FullName).ToList();
            foreach (var staff in activeStaff)
            {
                StaffList.Add(staff);
            }

            CasesList.Clear();
            var activeCases = db.Cases.OrderByDescending(c => c.CreatedAt).ToList();
            foreach (var c in activeCases)
            {
                CasesList.Add(c);
            }
        }

        public void OpenCreateModal()
        {
            ClearForm();
            IsEditMode = false;
            SelectedTask = null;
            IsTaskModalOpen = true;
        }

        public void OpenEditModal(CoreTask t)
        {
            SelectedTask = t;
            IsEditMode = true;
            FormTitle = t.Title;
            FormSelectedCase = CasesList.FirstOrDefault(c => c.CaseID == t.CaseID);
            FormAssignedTo = StaffList.FirstOrDefault(s => s.UserID == t.AssignedToID);
            FormStatus = t.Status;
            FormDueDate = t.DueDate;
            IsTaskModalOpen = true;
        }

        public void LoadTasks()
        {
            int? caseId = SelectedCaseFilter?.CaseID;
            int? assignedToId = SelectedStaffFilter?.UserID;
            var list = _taskService.GetTasks(caseId, assignedToId, StatusFilter);

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                string q = SearchQuery.ToLowerInvariant();
                list = list.Where(t => 
                    (t.Title != null && t.Title.ToLowerInvariant().Contains(q)) ||
                    (t.Case != null && t.Case.FileNumber != null && t.Case.FileNumber.ToLowerInvariant().Contains(q)) ||
                    (t.Case != null && t.Case.Title != null && t.Case.Title.ToLowerInvariant().Contains(q)) ||
                    (t.AssignedTo != null && t.AssignedTo.FullName != null && t.AssignedTo.FullName.ToLowerInvariant().Contains(q))
                ).ToList();
            }

            TasksList.Clear();
            foreach (var t in list)
            {
                TasksList.Add(t);
            }
        }

        private void ResetFilters()
        {
            SearchQuery = string.Empty;
            StatusFilter = "All";
            SelectedStaffFilter = null;
            SelectedCaseFilter = null;
            IsFilterModalOpen = false;
            LoadTasks();
        }

        private void ClearForm()
        {
            FormTitle = string.Empty;
            FormSelectedCase = null;
            FormAssignedTo = null;
            FormStatus = TaskStatuses.Pending;
            FormDueDate = DateTime.Today.AddDays(7);
        }

        private void ExecuteSaveTask()
        {
            if (IsEditMode)
            {
                ExecuteUpdateTask();
            }
            else
            {
                ExecuteCreateTask();
            }
        }

        private void ExecuteCreateTask()
        {
            if (string.IsNullOrWhiteSpace(FormTitle) || FormSelectedCase == null || FormAssignedTo == null)
            {
                MessageBox.Show("Please fill out all required fields: Task Title, Case, and Assigned Staff.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newTask = new CoreTask
            {
                CaseID = FormSelectedCase.CaseID,
                Title = FormTitle,
                AssignedToID = FormAssignedTo.UserID,
                DueDate = FormDueDate,
                Status = FormStatus
            };

            bool success = _taskService.CreateTask(newTask);
            if (success)
            {
                MessageBox.Show("Task created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                IsTaskModalOpen = false;
                ClearForm();
                LoadTasks();
            }
            else
            {
                MessageBox.Show("Failed to create task.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteUpdateTask()
        {
            if (SelectedTask == null) return;

            if (string.IsNullOrWhiteSpace(FormTitle) || FormSelectedCase == null || FormAssignedTo == null)
            {
                MessageBox.Show("Please fill out all required fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var updated = new CoreTask
            {
                TaskID = SelectedTask.TaskID,
                CaseID = FormSelectedCase.CaseID,
                Title = FormTitle,
                AssignedToID = FormAssignedTo.UserID,
                DueDate = FormDueDate,
                Status = FormStatus
            };

            bool success = _taskService.UpdateTask(updated);
            if (success)
            {
                MessageBox.Show("Task updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                IsTaskModalOpen = false;
                LoadTasks();
            }
            else
            {
                MessageBox.Show("Failed to update task.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteDeleteTask(object? parameter)
        {
            if (parameter is CoreTask t)
            {
                var result = MessageBox.Show($"Are you sure you want to delete task \"{t.Title}\"?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    if (_taskService.DeleteTask(t.TaskID))
                    {
                        if (IsTaskModalOpen && SelectedTask?.TaskID == t.TaskID)
                        {
                            IsTaskModalOpen = false;
                        }
                        LoadTasks();
                    }
                }
            }
        }

        private void ExecuteCycleStatus(object? parameter)
        {
            if (parameter is CoreTask t)
            {
                if (t.Status == TaskStatuses.Pending) t.Status = TaskStatuses.InProgress;
                else if (t.Status == TaskStatuses.InProgress) t.Status = TaskStatuses.Completed;
                else t.Status = TaskStatuses.Pending;

                bool success = _taskService.UpdateTask(t);
                if (success)
                {
                    LoadTasks();
                }
            }
        }
    }
}
