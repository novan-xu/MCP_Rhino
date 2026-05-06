using System.Windows;

namespace MCP_Rhino.Companion;

public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        if (!CompanionOptions.TryParse(e.Args, out CompanionOptions? options, out string error))
        {
            Environment.ExitCode = 2;
            if (!CompanionOptions.ContainsHelpFlag(e.Args))
            {
                MessageBox.Show(
                    error + Environment.NewLine + Environment.NewLine + CompanionOptions.Usage,
                    "MCP_Rhino Companion",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            Shutdown();
            return;
        }

        CompanionOptions parsedOptions = options
            ?? throw new InvalidOperationException("Companion options were not parsed.");

        if (parsedOptions.Help)
        {
            Environment.ExitCode = 0;
            Shutdown();
            return;
        }

        if (parsedOptions.ValidateOnly)
        {
            Environment.ExitCode = 0;
            Shutdown();
            return;
        }

        MainWindow = new MainWindow(parsedOptions);
        MainWindow.Show();
    }
}
