using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using LawCaseManagement.Core;

namespace LawCaseManagement.Wpf.ViewModels
{
    public class ClientsViewModel : ViewModelBase
    {
        private readonly IClientService _clientService;
        private Action<int>? _onNavigateToCase;

        private Client? _selectedClient;
        private string _searchQuery = string.Empty;

        // Form Fields
        private string _formFullName = string.Empty;
        private string _formAddress = string.Empty;
        private string _formPhone = string.Empty;
        private string _formEmail = string.Empty;

        public ObservableCollection<Client> ClientsList { get; } = new ObservableCollection<Client>();
        public ObservableCollection<Case> LinkedCasesList { get; } = new ObservableCollection<Case>();

        public Client? SelectedClient
        {
            get => _selectedClient;
            set
            {
                if (SetProperty(ref _selectedClient, value))
                {
                    OnPropertyChanged(nameof(HasSelectedClient));
                    LoadSelectedClientDetails();
                }
            }
        }

        public bool HasSelectedClient => SelectedClient != null;

        public string SearchQuery
        {
            get => _searchQuery;
            set => SetProperty(ref _searchQuery, value);
        }

        public string FormFullName { get => _formFullName; set => SetProperty(ref _formFullName, value); }
        public string FormAddress { get => _formAddress; set => SetProperty(ref _formAddress, value); }
        public string FormPhone { get => _formPhone; set => SetProperty(ref _formPhone, value); }
        public string FormEmail { get => _formEmail; set => SetProperty(ref _formEmail, value); }

        public ICommand SearchCommand { get; }
        public ICommand CreateClientCommand { get; }
        public ICommand UpdateClientCommand { get; }
        public ICommand ClearFormCommand { get; }
        public ICommand OpenCaseCommand { get; }

        public ClientsViewModel(
            IClientService? clientService = null,
            Action<int>? onNavigateToCase = null,
            IDbContextFactory<CaseDbContext>? contextFactory = null)
        {
            _clientService = clientService ?? new ClientService(contextFactory);
            _onNavigateToCase = onNavigateToCase;

            SearchCommand = new RelayCommand(LoadClients);
            CreateClientCommand = new RelayCommand(ExecuteCreateClient);
            UpdateClientCommand = new RelayCommand(ExecuteUpdateClient);
            ClearFormCommand = new RelayCommand(ClearForm);

            OpenCaseCommand = new RelayCommand<object>(param =>
            {
                if (param is Case c)
                {
                    _onNavigateToCase?.Invoke(c.CaseID);
                }
            });

            LoadClients();
        }

        public void SetNavigateCallback(Action<int> onNavigateToCase)
        {
            _onNavigateToCase = onNavigateToCase;
        }

        public void LoadClients()
        {
            var list = _clientService.GetClients(SearchQuery);
            ClientsList.Clear();
            foreach (var c in list)
            {
                ClientsList.Add(c);
            }
        }

        private void LoadSelectedClientDetails()
        {
            LinkedCasesList.Clear();
            if (SelectedClient == null)
            {
                ClearForm();
                return;
            }

            FormFullName = SelectedClient.FullName;
            FormAddress = SelectedClient.Address;
            FormPhone = SelectedClient.Phone;
            FormEmail = SelectedClient.Email;

            var cases = _clientService.GetCasesForClient(SelectedClient.ClientID);
            foreach (var c in cases)
            {
                LinkedCasesList.Add(c);
            }
        }

        private void ClearForm()
        {
            FormFullName = string.Empty;
            FormAddress = string.Empty;
            FormPhone = string.Empty;
            FormEmail = string.Empty;
            SelectedClient = null;
            LinkedCasesList.Clear();
        }

        private void ExecuteCreateClient()
        {
            if (string.IsNullOrWhiteSpace(FormFullName) || string.IsNullOrWhiteSpace(FormPhone))
            {
                MessageBox.Show("Please enter Full Name and Phone Number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var c = new Client
            {
                FullName = FormFullName,
                Address = FormAddress,
                Phone = FormPhone,
                Email = FormEmail
            };

            bool success = _clientService.CreateClient(c);
            if (success)
            {
                MessageBox.Show("Client profile created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadClients();
            }
            else
            {
                MessageBox.Show("Failed to create client profile.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteUpdateClient()
        {
            if (SelectedClient == null) return;

            if (string.IsNullOrWhiteSpace(FormFullName) || string.IsNullOrWhiteSpace(FormPhone))
            {
                MessageBox.Show("Please enter Full Name and Phone Number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var c = new Client
            {
                ClientID = SelectedClient.ClientID,
                FullName = FormFullName,
                Address = FormAddress,
                Phone = FormPhone,
                Email = FormEmail
            };

            bool success = _clientService.UpdateClient(c);
            if (success)
            {
                MessageBox.Show("Client profile updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadClients();
            }
            else
            {
                MessageBox.Show("Failed to update client profile.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
