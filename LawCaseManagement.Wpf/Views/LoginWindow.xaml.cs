using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using LawCaseManagement.Wpf.ViewModels;

namespace LawCaseManagement.Wpf.Views
{
    public partial class LoginWindow : Window
    {
        private readonly IServiceProvider? _serviceProvider;

        // Constructor with DI
        public LoginWindow(LoginViewModel viewModel, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;

            viewModel.SetLoginCallback(OnLoginSuccess);
            DataContext = viewModel;
        }

        // Fallback parameterless constructor for XAML designer
        public LoginWindow()
        {
            InitializeComponent();
            DataContext = new LoginViewModel(null, OnLoginSuccess);
        }

        private void OnLoginSuccess()
        {
            MainWindow mainWindow;
            if (_serviceProvider != null)
            {
                mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            }
            else
            {
                mainWindow = new MainWindow();
            }

            mainWindow.Show();
            this.Close();
        }
    }
}
