using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LawCaseManagement.Core;
using LawCaseManagement.Wpf.ViewModels;

namespace LawCaseManagement.Wpf.Views
{
    public partial class CasesView : UserControl
    {
        public CasesView()
        {
            InitializeComponent();
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.Item is Case c && DataContext is CasesViewModel vm)
            {
                vm.OpenEditModal(c);
            }
        }

        private void ViewButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Case c && DataContext is CasesViewModel vm)
            {
                vm.OpenEditModal(c);
            }
        }
    }
}
