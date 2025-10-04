using System;
using System.Linq;
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
            if (exception is AggregateException aggregateEx && aggregateEx.InnerException is UserMessageException)
                exception = aggregateEx.InnerException;
            var message = exception is UserMessageException ? exception.Message : exception.ToString();

            static MessageBoxResult ShowErrorBox(string message)
            {
                var topWindow = App.Current.MainWindow;
                while (true)
                {
                    var child = topWindow.OwnedWindows.OfType<Window>().FirstOrDefault(w => w.IsVisible);
                    if (child == null)
                        break;
                    topWindow = child;
                }
                return MessageBox.Show(topWindow, message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            try
            {
                Log.Write(message);
                if (Application.Current.Dispatcher == Dispatcher.CurrentDispatcher)
                    ShowErrorBox(message);
                else
                    Application.Current.Dispatcher.BeginInvoke(() => ShowErrorBox(message));
            }
            catch 
            {
                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void LogException(Exception exception)
        {
            Log.Write(exception.ToString());
        }
    }
}
