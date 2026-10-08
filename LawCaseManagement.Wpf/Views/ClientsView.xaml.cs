using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LawCaseManagement.Core;
using LawCaseManagement.Wpf.ViewModels;

namespace LawCaseManagement.Wpf.Views
{
    public partial class ClientsView : UserControl
    {
        public ClientsView()
        {
            InitializeComponent();
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.Item is Client c && DataContext is ClientsViewModel vm)
            {
                vm.OpenEditModal(c);
            }
        }

        private void ViewButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Client c && DataContext is ClientsViewModel vm)
            {
                vm.OpenEditModal(c);
            }
        }
    }
}
