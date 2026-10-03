using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using LawCaseManagement.Core;
using CoreTask = LawCaseManagement.Core.Task;

namespace LawCaseManagement.Wpf.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly IDbContextFactory<CaseDbContext>? _contextFactory;
        private Action<int>? _onNavigateToCase;
        private Action? _onNavigateToTasks;

        private int _openCasesCount;
        private int _inProgressCasesCount;
        private int _closedCasesCount;
        private int _totalCasesCount;

        public int OpenCasesCount
        {
            get => _openCasesCount;
            set => SetProperty(ref _openCasesCount, value);
        }

        public int InProgressCasesCount
        {
            get => _inProgressCasesCount;
            set => SetProperty(ref _inProgressCasesCount, value);
        }

        public int ClosedCasesCount
        {
            get => _closedCasesCount;
            set => SetProperty(ref _closedCasesCount, value);
        }

        public int TotalCasesCount
        {
            get => _totalCasesCount;
            set => SetProperty(ref _totalCasesCount, value);
        }

        public ObservableCollection<Case> MyCases { get; } = new ObservableCollection<Case>();
        public ObservableCollection<CoreTask> MyTasks { get; } = new ObservableCollection<CoreTask>();
        public ObservableCollection<AuditLog> RecentActivity { get; } = new ObservableCollection<AuditLog>();

        public ICommand OpenCaseCommand { get; }
        public ICommand OpenTasksCommand { get; }

        public DashboardViewModel(
            IDbContextFactory<CaseDbContext>? contextFactory = null,
            Action<int>? onNavigateToCase = null,
            Action? onNavigateToTasks = null)
        {
            _contextFactory = contextFactory;
            _onNavigateToCase = onNavigateToCase;
            _onNavigateToTasks = onNavigateToTasks;

            OpenCaseCommand = new RelayCommand<object>(param =>
            {
                if (param is Case c)
                {
                    _onNavigateToCase?.Invoke(c.CaseID);
                }
            });

            OpenTasksCommand = new RelayCommand(() => _onNavigateToTasks?.Invoke());

            LoadDashboardData();
        }

        public void SetNavigationCallbacks(Action<int> onNavigateToCase, Action onNavigateToTasks)
        {
            _onNavigateToCase = onNavigateToCase;
            _onNavigateToTasks = onNavigateToTasks;
        }

        private CaseDbContext CreateDbContext() => _contextFactory != null ? _contextFactory.CreateDbContext() : new CaseDbContext();

        public void LoadDashboardData()
        {
            var currentUser = AuthService.CurrentUser;
            if (currentUser == null) return;

            using var db = CreateDbContext();

            // 1. Get Cases Counts
            var allCases = db.Cases.ToList();
            TotalCasesCount = allCases.Count;
            OpenCasesCount = allCases.Count(c => c.Status == CaseStatuses.Open);
            InProgressCasesCount = allCases.Count(c => c.Status == CaseStatuses.InProgress);
            ClosedCasesCount = allCases.Count(c => c.Status == CaseStatuses.Closed || c.Status == CaseStatuses.Archived);

            // 2. Load My Cases (where logged-in user is Lawyer or Paralegal, or all cases for Admin)
            MyCases.Clear();
            var userCases = db.Cases
                .Include(c => c.Lawyer)
                .Include(c => c.Paralegal)
                .Include(c => c.CaseClients).ThenInclude(cc => cc.Client)
                .Where(c => currentUser.Role == Roles.Admin || c.LawyerID == currentUser.UserID || c.ParalegalID == currentUser.UserID)
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .ToList();

            foreach (var c in userCases)
            {
                MyCases.Add(c);
            }

            // 3. Load My Tasks (assigned to logged-in user, incomplete)
            MyTasks.Clear();
            var userTasks = db.Tasks
                .Include(t => t.Case)
                .Where(t => t.AssignedToID == currentUser.UserID && t.Status != TaskStatuses.Completed)
                .OrderBy(t => t.DueDate)
                .Take(5)
                .ToList();

            foreach (var t in userTasks)
            {
                MyTasks.Add(t);
            }

            // 4. Load Recent System Activity (Audit Logs)
            RecentActivity.Clear();
            var logs = db.AuditLogs
                .Include(al => al.User)
                .OrderByDescending(al => al.Timestamp)
                .Take(8)
                .ToList();

            foreach (var log in logs)
            {
                RecentActivity.Add(log);
            }
        }
    }
}
