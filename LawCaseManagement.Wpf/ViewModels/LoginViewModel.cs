using System;
using System.Windows.Controls;
using System.Windows.Input;
using LawCaseManagement.Core;

namespace LawCaseManagement.Wpf.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private string _username = string.Empty;
        private string _errorMessage = string.Empty;
        private readonly IAuthService _authService;
        private Action? _onLoginSuccess;

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand LoginCommand { get; }

        public LoginViewModel(IAuthService? authService = null, Action? onLoginSuccess = null)
        {
            _authService = authService ?? new AuthService();
            _onLoginSuccess = onLoginSuccess;
            LoginCommand = new RelayCommand(ExecuteLogin);
        }

        public void SetLoginCallback(Action onLoginSuccess)
        {
            _onLoginSuccess = onLoginSuccess;
        }

        private void ExecuteLogin(object? parameter)
        {
            ErrorMessage = string.Empty;

            var passwordBox = parameter as PasswordBox;
            string? password = passwordBox?.Password;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Please enter both username and password.";
                return;
            }

            bool authenticated = _authService.Authenticate(Username, password);
            if (authenticated)
            {
                _onLoginSuccess?.Invoke();
            }
            else
            {
                ErrorMessage = "Invalid username or password, or account is inactive.";
            }
        }
    }
}
