using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace PhotoLocator.Helpers
{
    static class ExceptionHandler
    {
        public static void ShowException(Exception exception)
        {
            if (exception is OperationCanceledException || exception.InnerException is OperationCanceledException)
                return;
            var message = exception is UserMessageException ? exception.Message : exception.ToString();
            try
            {
                if (Application.Current.Dispatcher == Dispatcher.CurrentDispatcher)
                    MessageBox.Show(App.Current.MainWindow, message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    Application.Current.Dispatcher.BeginInvoke(() =>
                        MessageBox.Show(App.Current.MainWindow, message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
            catch 
            {
                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void LogException(Exception exception)
        {
            Debug.WriteLine(exception.ToString());
        }
    }
}
