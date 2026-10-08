using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using LawCaseManagement.Core;

namespace LawCaseManagement.Wpf.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentView;
        private readonly IAuthService _authService;
        private readonly AppConfig? _appConfig;
        public Action? OnLogout { get; set; }

        private bool _isSidebarCollapsed;
        private bool _isSignOutModalOpen;
        private string _currentSection = "Dashboard";

        // Sync & Connection state (§4)
        private bool _isOnline;
        private bool _isSyncing;
        private int _pendingSyncCount = 0;
        private bool _hasSyncConflict = false;

        public User? CurrentUser => AuthService.CurrentUser;
        
        public bool IsAdmin => CurrentUser?.Role == Roles.Admin;
        public bool IsLawyerOrAdmin => CurrentUser?.Role == Roles.Admin || CurrentUser?.Role == Roles.Lawyer;

        public ThemeManager Theme => ThemeManager.Instance;

        public ViewModelBase CurrentView
        {
            get => _currentView;
            set
            {
                if (SetProperty(ref _currentView, value))
                {
                    UpdateCurrentSection();
                }
            }
        }

        public string CurrentSection
        {
            get => _currentSection;
            set => SetProperty(ref _currentSection, value);
        }

        public bool IsSidebarCollapsed
        {
            get => _isSidebarCollapsed;
            set
            {
                if (SetProperty(ref _isSidebarCollapsed, value))
                {
                    OnPropertyChanged(nameof(SidebarWidth));
                    OnPropertyChanged(nameof(SidebarToggleIcon));
                    OnPropertyChanged(nameof(SidebarToggleTooltip));
                    SaveSidebarState(value);
                }
            }
        }

        public double SidebarWidth => IsSidebarCollapsed ? 68 : 220;
        public string SidebarToggleIcon => IsSidebarCollapsed ? "▶" : "◀";
        public string SidebarToggleTooltip => IsSidebarCollapsed ? "Expand Sidebar" : "Collapse Sidebar";

        public bool IsSignOutModalOpen
        {
            get => _isSignOutModalOpen;
            set => SetProperty(ref _isSignOutModalOpen, value);
        }

        // Offline & Sync Properties (§4)
        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                if (SetProperty(ref _isOnline, value))
                {
                    OnPropertyChanged(nameof(ShowSyncBanner));
                    OnPropertyChanged(nameof(SyncStatusText));
                    OnPropertyChanged(nameof(SyncStatusDot));
                }
            }
        }

        public bool IsSyncing
        {
            get => _isSyncing;
            set
            {
                if (SetProperty(ref _isSyncing, value))
                {
                    OnPropertyChanged(nameof(ShowSyncBanner));
                    OnPropertyChanged(nameof(SyncBannerMessage));
                }
            }
        }

        public int PendingSyncCount
        {
            get => _pendingSyncCount;
            set
            {
                if (SetProperty(ref _pendingSyncCount, value))
                {
                    OnPropertyChanged(nameof(ShowSyncBanner));
                    OnPropertyChanged(nameof(SyncBannerMessage));
                }
            }
        }

        public bool HasSyncConflict
        {
            get => _hasSyncConflict;
            set
            {
                if (SetProperty(ref _hasSyncConflict, value))
                {
                    OnPropertyChanged(nameof(ShowSyncBanner));
                    OnPropertyChanged(nameof(SyncBannerMessage));
                }
            }
        }

        public bool ShowSyncBanner => !IsOnline || IsSyncing || HasSyncConflict || PendingSyncCount > 0;

        public string SyncStatusDot => IsOnline ? "🟢" : "🟡";
        public string SyncStatusText => IsOnline ? "Connected to server" : "Offline mode (local data)";

        public string SyncBannerMessage
        {
            get
            {
                if (HasSyncConflict)
                    return "⚠ Sync conflicts detected — review required by administrator";
                if (IsSyncing)
                    return "🔄 Syncing changes with office server…";
                if (!IsOnline && PendingSyncCount > 0)
                    return $"🟡 Offline — {PendingSyncCount} changes pending sync when reconnected";
                if (!IsOnline)
                    return "🟡 Offline — local changes will sync automatically when reconnected";
                return string.Empty;
            }
        }

        // ViewModels
        public DashboardViewModel DashboardVM { get; }
        public CasesViewModel CasesVM { get; }
        public ClientsViewModel ClientsVM { get; }
        public TasksViewModel TasksVM { get; }
        public AdminViewModel AdminVM { get; }

        // Navigation Commands
        public ICommand ShowDashboardCommand { get; }
        public ICommand ShowCasesCommand { get; }
        public ICommand ShowClientsCommand { get; }
        public ICommand ShowTasksCommand { get; }
        public ICommand ShowAdminCommand { get; }

        // Sidebar & Theme Commands
        public ICommand ToggleSidebarCommand { get; }
        public ICommand ToggleThemeCommand { get; }

        // Sign Out Commands
        public ICommand PromptSignOutCommand { get; }
        public ICommand CancelSignOutCommand { get; }
        public ICommand ConfirmSignOutCommand { get; }

        public MainViewModel(
            IAuthService? authService = null,
            AppConfig? appConfig = null,
            DashboardViewModel? dashboardVM = null,
            CasesViewModel? casesVM = null,
            ClientsViewModel? clientsVM = null,
            TasksViewModel? tasksVM = null,
            AdminViewModel? adminVM = null,
            Action? onLogout = null)
        {
            _authService = authService ?? new AuthService();
            _appConfig = appConfig;
            OnLogout = onLogout;

            // Load persisted sidebar state
            _isSidebarCollapsed = LoadSidebarState();

            // Set initial connection state
            _isOnline = _appConfig?.Database.Provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) == true;

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
            _currentSection = "Dashboard";

            // Commands
            ShowDashboardCommand = new RelayCommand(() => { DashboardVM.LoadDashboardData(); CurrentView = DashboardVM; });
            ShowCasesCommand = new RelayCommand(() => { CasesVM.LoadCases(); CurrentView = CasesVM; });
            ShowClientsCommand = new RelayCommand(() => { ClientsVM.LoadClients(); CurrentView = ClientsVM; });
            ShowTasksCommand = new RelayCommand(() => { TasksVM.LoadTasks(); CurrentView = TasksVM; });
            ShowAdminCommand = new RelayCommand(() => { AdminVM.LoadData(); CurrentView = AdminVM; });

            ToggleSidebarCommand = new RelayCommand(() => IsSidebarCollapsed = !IsSidebarCollapsed);
            ToggleThemeCommand = new RelayCommand(() => ThemeManager.Instance.ToggleTheme());

            PromptSignOutCommand = new RelayCommand(() => IsSignOutModalOpen = true);
            CancelSignOutCommand = new RelayCommand(() => IsSignOutModalOpen = false);
            ConfirmSignOutCommand = new RelayCommand(ExecuteLogout);
        }

        private void UpdateCurrentSection()
        {
            if (CurrentView is DashboardViewModel) CurrentSection = "Dashboard";
            else if (CurrentView is CasesViewModel) CurrentSection = "Cases";
            else if (CurrentView is ClientsViewModel) CurrentSection = "Clients";
            else if (CurrentView is TasksViewModel) CurrentSection = "Tasks";
            else if (CurrentView is AdminViewModel) CurrentSection = "Admin";
        }

        public void NavigateToCase(int caseId)
        {
            CasesVM.LoadCases();
            var target = CasesVM.CasesList.FirstOrDefault(c => c.CaseID == caseId);
            if (target != null)
            {
                CasesVM.OpenEditModal(target);
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
            IsSignOutModalOpen = false;
            _authService.Logout();
            OnLogout?.Invoke();
        }

        private string GetSidebarConfigPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "LawCaseManagement");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "sidebar_settings.json");
        }

        private bool LoadSidebarState()
        {
            try
            {
                string path = GetSidebarConfigPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("IsCollapsed", out var prop))
                    {
                        return prop.GetBoolean();
                    }
                }
            }
            catch { }
            return false;
        }

        private void SaveSidebarState(bool isCollapsed)
        {
            try
            {
                string path = GetSidebarConfigPath();
                string json = JsonSerializer.Serialize(new { IsCollapsed = isCollapsed });
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }
}
