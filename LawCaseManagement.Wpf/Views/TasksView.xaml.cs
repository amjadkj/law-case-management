using System.Windows.Controls;
using System.Windows.Input;
using LawCaseManagement.Core;
using LawCaseManagement.Wpf.ViewModels;
using CoreTask = LawCaseManagement.Core.Task;

namespace LawCaseManagement.Wpf.Views
{
    public partial class TasksView : UserControl
    {
        public TasksView()
        {
            InitializeComponent();
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.Item is CoreTask t && DataContext is TasksViewModel vm)
            {
                vm.OpenEditModal(t);
            }
        }
    }
}
