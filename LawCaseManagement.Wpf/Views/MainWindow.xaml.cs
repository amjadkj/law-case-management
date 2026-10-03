using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using LawCaseManagement.Wpf.ViewModels;

namespace LawCaseManagement.Wpf.Views
{
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider? _serviceProvider;
        private SessionTimeoutManager _sessionTimeoutManager;

        // Constructor with DI
        public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            DataContext = viewModel;

            _sessionTimeoutManager = new SessionTimeoutManager(TimeSpan.FromMinutes(15), Logout);
            _sessionTimeoutManager.Start();

            this.Closed += (s, e) => _sessionTimeoutManager?.Stop();
        }

        // Fallback constructor
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel(onLogout: Logout);

            _sessionTimeoutManager = new SessionTimeoutManager(TimeSpan.FromMinutes(15), Logout);
            _sessionTimeoutManager.Start();

            this.Closed += (s, e) => _sessionTimeoutManager?.Stop();
        }

        private void Logout()
        {
            _sessionTimeoutManager?.Stop();

            LoginWindow loginWindow;
            if (_serviceProvider != null)
            {
                loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            }
            else
            {
                loginWindow = new LoginWindow();
            }

            loginWindow.Show();
            this.Close();
        }
    }
}
