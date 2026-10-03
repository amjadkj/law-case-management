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

        private string _statusFilter = "All";
        private User? _selectedStaffFilter;
        private Case? _selectedCaseFilter;
        private CoreTask? _selectedTask;

        // New Task Form
        private string _newTaskTitle = string.Empty;
        private Case? _newTaskSelectedCase;
        private User? _newTaskAssignedTo;
        private DateTime _newTaskDueDate = DateTime.Today.AddDays(7);

        public ObservableCollection<CoreTask> TasksList { get; } = new ObservableCollection<CoreTask>();
        public ObservableCollection<User> StaffList { get; } = new ObservableCollection<User>();
        public ObservableCollection<Case> CasesList { get; } = new ObservableCollection<Case>();

        public string StatusFilter
        {
            get => _statusFilter;
            set
            {
                if (SetProperty(ref _statusFilter, value))
                {
                    LoadTasks();
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
                    LoadTasks();
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
                    LoadTasks();
                }
            }
        }

        public CoreTask? SelectedTask
        {
            get => _selectedTask;
            set => SetProperty(ref _selectedTask, value);
        }

        // Form Bindings
        public string NewTaskTitle { get => _newTaskTitle; set => SetProperty(ref _newTaskTitle, value); }
        public Case? NewTaskSelectedCase { get => _newTaskSelectedCase; set => SetProperty(ref _newTaskSelectedCase, value); }
        public User? NewTaskAssignedTo { get => _newTaskAssignedTo; set => SetProperty(ref _newTaskAssignedTo, value); }
        public DateTime NewTaskDueDate { get => _newTaskDueDate; set => SetProperty(ref _newTaskDueDate, value); }

        public ICommand CycleStatusCommand { get; }
        public ICommand RefreshTasksCommand { get; }
        public ICommand CreateTaskCommand { get; }
        public ICommand DeleteTaskCommand { get; }

        public TasksViewModel(
            ITaskService? taskService = null,
            ICaseService? caseService = null,
            IDbContextFactory<CaseDbContext>? contextFactory = null)
        {
            _contextFactory = contextFactory;
            _taskService = taskService ?? new TaskService(contextFactory);
            _caseService = caseService ?? new CaseService(contextFactory);

            CycleStatusCommand = new RelayCommand(ExecuteCycleStatus);
            RefreshTasksCommand = new RelayCommand(LoadTasks);
            CreateTaskCommand = new RelayCommand(ExecuteCreateTask);
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

        public void LoadTasks()
        {
            int? caseId = SelectedCaseFilter?.CaseID;
            int? assignedToId = SelectedStaffFilter?.UserID;
            var list = _taskService.GetTasks(caseId, assignedToId, StatusFilter);

            TasksList.Clear();
            foreach (var t in list)
            {
                TasksList.Add(t);
            }
        }

        private void ExecuteCreateTask()
        {
            if (string.IsNullOrWhiteSpace(NewTaskTitle) || NewTaskSelectedCase == null || NewTaskAssignedTo == null)
            {
                MessageBox.Show("Please fill out all required fields: Task Title, Case, and Assigned Staff.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newTask = new CoreTask
            {
                CaseID = NewTaskSelectedCase.CaseID,
                Title = NewTaskTitle,
                AssignedToID = NewTaskAssignedTo.UserID,
                DueDate = NewTaskDueDate,
                Status = TaskStatuses.Pending
            };

            bool success = _taskService.CreateTask(newTask);
            if (success)
            {
                NewTaskTitle = string.Empty;
                NewTaskSelectedCase = null;
                NewTaskAssignedTo = null;
                NewTaskDueDate = DateTime.Today.AddDays(7);
                LoadTasks();
            }
            else
            {
                MessageBox.Show("Failed to create task.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
