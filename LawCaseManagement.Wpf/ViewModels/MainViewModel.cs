using System;
using System.Linq;
using System.Windows.Input;
using LawCaseManagement.Core;

namespace LawCaseManagement.Wpf.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentView;
        private readonly IAuthService _authService;
        private readonly Action? _onLogout;

        public User? CurrentUser => AuthService.CurrentUser;
        
        public bool IsAdmin => CurrentUser?.Role == Roles.Admin;
        public bool IsLawyerOrAdmin => CurrentUser?.Role == Roles.Admin || CurrentUser?.Role == Roles.Lawyer;

        public ViewModelBase CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        // ViewModels
        public DashboardViewModel DashboardVM { get; }
        public CasesViewModel CasesVM { get; }
        public ClientsViewModel ClientsVM { get; }
        public TasksViewModel TasksVM { get; }
        public AdminViewModel AdminVM { get; }

        public ICommand ShowDashboardCommand { get; }
        public ICommand ShowCasesCommand { get; }
        public ICommand ShowClientsCommand { get; }
        public ICommand ShowTasksCommand { get; }
        public ICommand ShowAdminCommand { get; }
        public ICommand LogoutCommand { get; }

        public MainViewModel(
            IAuthService? authService = null,
            DashboardViewModel? dashboardVM = null,
            CasesViewModel? casesVM = null,
            ClientsViewModel? clientsVM = null,
            TasksViewModel? tasksVM = null,
            AdminViewModel? adminVM = null,
            Action? onLogout = null)
        {
            _authService = authService ?? new AuthService();
            _onLogout = onLogout;

            // Use injected or create default
            CasesVM = casesVM ?? new CasesViewModel();
            TasksVM = tasksVM ?? new TasksViewModel();
            AdminVM = adminVM ?? new AdminViewModel();
            DashboardVM = dashboardVM ?? new DashboardViewModel();
            ClientsVM = clientsVM ?? new ClientsViewModel();

            DashboardVM.SetNavigationCallbacks(
                onNavigateToCase: NavigateToCase,
                onNavigateToTasks: NavigateToTasks
            );

            ClientsVM.SetNavigateCallback(
                onNavigateToCase: NavigateToCase
            );

            // Set Initial View
            _currentView = DashboardVM;

            // Commands
            ShowDashboardCommand = new RelayCommand(() => { DashboardVM.LoadDashboardData(); CurrentView = DashboardVM; });
            ShowCasesCommand = new RelayCommand(() => { CasesVM.LoadCases(); CurrentView = CasesVM; });
            ShowClientsCommand = new RelayCommand(() => { ClientsVM.LoadClients(); CurrentView = ClientsVM; });
            ShowTasksCommand = new RelayCommand(() => { TasksVM.LoadTasks(); CurrentView = TasksVM; });
            ShowAdminCommand = new RelayCommand(() => { AdminVM.LoadData(); CurrentView = AdminVM; });
            LogoutCommand = new RelayCommand(ExecuteLogout);
        }

        public void NavigateToCase(int caseId)
        {
            CasesVM.LoadCases();
            var target = CasesVM.CasesList.FirstOrDefault(c => c.CaseID == caseId);
            if (target != null)
            {
                CasesVM.SelectedCase = target;
            }
            CurrentView = CasesVM;
        }

        public void NavigateToTasks()
        {
            TasksVM.LoadTasks();
            CurrentView = TasksVM;
        }

        private void ExecuteLogout()
        {
            _authService.Logout();
            _onLogout?.Invoke();
        }
    }
}
