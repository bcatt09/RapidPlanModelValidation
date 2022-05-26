using System.Windows;
using System.Windows.Controls;

namespace RapidPlanModelValidation
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
			//SearchTextBox.Focus();
		}

		private void Structure_OnChecked(object checkBoxObject, RoutedEventArgs e)
		{
			(DataContext as MainViewModel).AddDVHCurve((checkBoxObject as CheckBox).Content as string);
		}

		private void Structure_OnUnchecked(object checkBoxObject, RoutedEventArgs e)
		{
			(DataContext as MainViewModel).RemoveDVHCurve((checkBoxObject as CheckBox).Content as string);
		}
	}
}
