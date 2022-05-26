using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace RapidPlanModelValidation
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private EsapiService _esapiService;

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
			try
			{
				_esapiService = new EsapiService();
				var dialogService = new DialogService(this);
				var viewModel = new MainViewModel(_esapiService, dialogService);
				var window = new MainWindow(viewModel);
				window.Show();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"App_OnStartup\n\n{ex.Message}\n\n{ex.InnerException}\n\n{ex.StackTrace}", "Failed on Startup", MessageBoxButton.OK, MessageBoxImage.Error);
			}
        }

        // The EsapiService must be disposed before exiting;
        // otherwise, an exception from ESAPI is thrown
        private void App_OnExit(object sender, ExitEventArgs e)
        {
			MainViewModel.DeleteWarningLog();
            _esapiService.Dispose();
        }
    }
}
