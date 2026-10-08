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
        private string _linkedCaseFilter = "All"; // "All", "WithCases", "WithoutCases"

        // Modal states (§2.7)
        private bool _isClientModalOpen;
        private bool _isFilterModalOpen;
        private bool _isEditMode;

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

        // Modals
        public bool IsClientModalOpen
        {
            get => _isClientModalOpen;
            set => SetProperty(ref _isClientModalOpen, value);
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

        public string ModalTitle => IsEditMode ? $"Client Profile: {FormFullName}" : "New Client";
        public string SaveButtonText => IsEditMode ? "Save Changes" : "Create Client";

        // Scoped Search & Filter (§2.5)
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    LoadClients();
                }
            }
        }

        public string LinkedCaseFilter
        {
            get => _linkedCaseFilter;
            set
            {
                if (SetProperty(ref _linkedCaseFilter, value))
                {
                    OnPropertyChanged(nameof(HasActiveFilters));
                }
            }
        }

        public bool HasActiveFilters => LinkedCaseFilter != "All" && !string.IsNullOrEmpty(LinkedCaseFilter);

        // Form Properties
        public string FormFullName { get => _formFullName; set => SetProperty(ref _formFullName, value); }
        public string FormAddress { get => _formAddress; set => SetProperty(ref _formAddress, value); }
        public string FormPhone { get => _formPhone; set => SetProperty(ref _formPhone, value); }
        public string FormEmail { get => _formEmail; set => SetProperty(ref _formEmail, value); }

        // Commands
        public ICommand SearchCommand { get; }
        public ICommand ToggleFilterModalCommand { get; }
        public ICommand ApplyFiltersCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand OpenCreateModalCommand { get; }
        public ICommand CloseClientModalCommand { get; }
        public ICommand SaveClientCommand { get; }
        public ICommand OpenCaseCommand { get; }

        public ClientsViewModel(
            IClientService? clientService = null,
            Action<int>? onNavigateToCase = null,
            IDbContextFactory<CaseDbContext>? contextFactory = null)
        {
            _clientService = clientService ?? new ClientService(contextFactory);
            _onNavigateToCase = onNavigateToCase;

            SearchCommand = new RelayCommand(LoadClients);
            ToggleFilterModalCommand = new RelayCommand(() => IsFilterModalOpen = !IsFilterModalOpen);
            ApplyFiltersCommand = new RelayCommand(() => { IsFilterModalOpen = false; LoadClients(); });
            ClearFiltersCommand = new RelayCommand(ResetFilters);

            OpenCreateModalCommand = new RelayCommand(OpenCreateModal);
            CloseClientModalCommand = new RelayCommand(() => IsClientModalOpen = false);
            SaveClientCommand = new RelayCommand(ExecuteSaveClient);

            OpenCaseCommand = new RelayCommand<object>(param =>
            {
                if (param is Case c)
                {
                    IsClientModalOpen = false;
                    _onNavigateToCase?.Invoke(c.CaseID);
                }
            });

            LoadClients();
        }

        public void SetNavigateCallback(Action<int> onNavigateToCase)
        {
            _onNavigateToCase = onNavigateToCase;
        }

        public void OpenCreateModal()
        {
            ClearForm();
            IsEditMode = false;
            SelectedClient = null;
            IsClientModalOpen = true;
        }

        public void OpenEditModal(Client c)
        {
            SelectedClient = c;
            IsEditMode = true;
            IsClientModalOpen = true;
        }

        public void LoadClients()
        {
            var list = _clientService.GetClients(SearchQuery);

            if (LinkedCaseFilter == "WithCases")
            {
                list = list.Where(c => c.CaseClients.Count > 0).ToList();
            }
            else if (LinkedCaseFilter == "WithoutCases")
            {
                list = list.Where(c => c.CaseClients.Count == 0).ToList();
            }

            ClientsList.Clear();
            foreach (var c in list)
            {
                ClientsList.Add(c);
            }
        }

        private void ResetFilters()
        {
            SearchQuery = string.Empty;
            LinkedCaseFilter = "All";
            IsFilterModalOpen = false;
            LoadClients();
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
            LinkedCasesList.Clear();
        }

        private void ExecuteSaveClient()
        {
            if (IsEditMode)
            {
                ExecuteUpdateClient();
            }
            else
            {
                ExecuteCreateClient();
            }
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
                IsClientModalOpen = false;
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
                IsClientModalOpen = false;
                LoadClients();
            }
            else
            {
                MessageBox.Show("Failed to update client profile.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
