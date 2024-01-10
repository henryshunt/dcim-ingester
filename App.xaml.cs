using DcimIngester.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DcimIngester
{
    public partial class App : Application
    {
        private TaskbarIcon? taskbarIcon = null;
        private MainWindow? mainWindow = null;
        private Settings? settingsWindow = null;


        private void Application_Startup(object sender, StartupEventArgs e)
        {
            taskbarIcon = new()
            {
                ToolTipText = "DCIM Ingester",
                ContextMenu = (ContextMenu)FindResource("TrayContextMenu")
            };

            using (Stream stream = GetResourceStream(new Uri(
                "pack://application:,,,/DcimIngester;component/Icon.ico")).Stream)
            {
                taskbarIcon.Icon = new Icon(stream);
            }

            taskbarIcon.Visibility = Visibility.Visible;

            // Set destination to user's pictures directory on first launch
            if (DcimIngester.Properties.Settings.Default.DestDirectory.Length == 0)
            {
                DcimIngester.Properties.Settings.Default.DestDirectory =
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                DcimIngester.Properties.Settings.Default.Save();
            }

            mainWindow = new MainWindow();

            // Need to show the window to get the Loaded event to trigger
            mainWindow.Show();
            mainWindow.Hide();
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (settingsWindow == null)
            {
                settingsWindow = new Settings();
                settingsWindow.Closed += (sender, e) => settingsWindow = null;
                settingsWindow.Show();
            }
            else settingsWindow.Focus();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (mainWindow!.ActiveIngestCount == 0)
            {
                // Icon stays visible after shutdown (until hovered over) without this
                taskbarIcon!.Visibility = Visibility.Collapsed;

                Shutdown();
            }
            else
            {
                // MessageBox immediately closes without using Task
                Task.Run(() =>
                {
                    MessageBox.Show("Wait for ingests to finish before exiting.", "DCIM Ingester",
                        MessageBoxButton.OK, MessageBoxImage.Exclamation, MessageBoxResult.OK,
                        MessageBoxOptions.DefaultDesktopOnly);
                });
            }
        }
    }
}
