using System;
using System.Windows;
using System.Windows.Threading;

namespace PedalTelemetry
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Handle unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            var message = exception != null 
                ? $"Unhandled Exception: {exception.Message}\n\nStack Trace:\n{exception.StackTrace}"
                : "An unknown error occurred.";
            
            MessageBox.Show(message, "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var message = $"Unhandled UI Exception: {e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}";
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // Prevent app from crashing
        }
    }
}
