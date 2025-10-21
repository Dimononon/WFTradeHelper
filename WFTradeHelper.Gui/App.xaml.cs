using System.Configuration;
using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace WFTradeHelper.Gui;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
    }

    void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var sb = new StringBuilder();
        sb.AppendLine("An unhandled exception occurred!");
        sb.AppendLine("=").AppendLine();

        Exception ex = e.Exception;
        int level = 0;
        while (ex != null)
        {
            sb.AppendLine($"Level: {level++}");
            sb.AppendLine($"Type: {ex.GetType().FullName}");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine($"StackTrace:\n{ex.StackTrace}");
            sb.AppendLine("=").AppendLine();
            ex = ex.InnerException;
        }

        string logFilePath = Path.Combine(AppContext.BaseDirectory, "crash_log.txt");
        File.WriteAllText(logFilePath, sb.ToString());

        MessageBox.Show("A critical error occurred. Please check the crash_log.txt file for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

        e.Handled = true;
        Current.Shutdown();
    }
}

