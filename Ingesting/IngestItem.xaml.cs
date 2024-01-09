using DcimIngester.Ingesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static DcimIngester.Ingesting.IngestTask;
using static DcimIngester.Utilities;

namespace DcimIngester.Controls
{
    /// <summary>
    /// Represents the user interface for an ingest operation.
    /// </summary>
    public partial class IngestItem : UserControl
    {
        /// <summary>
        /// The ingest task for the item to execute.
        /// </summary>
        private readonly IngestTask task;

        /// <summary>
        /// Gets the letter of the volume to ingest from.
        /// </summary>
        public char VolumeLetter => task.Work.VolumeLetter;

        /// <summary>
        /// Gets the status of the ingest operation.
        /// </summary>
        public IngestTaskStatus Status => task.Status;

        /// <summary>
        /// Used to cancel the <see cref="System.Threading.Tasks.Task"/> that does the actual ingesting.
        /// </summary>
        private readonly CancellationTokenSource cancelSource = new();

        /// <summary>
        /// The directory that the first file was ingested into.
        /// </summary>
        private string? firstIngestDir = null;

        /// <summary>
        /// The number of ingested files that were sorted into directories by date taken.
        /// </summary>
        private int ingestedCount = 0;

        /// <summary>
        /// Occurs when the user dismisses the item.
        /// </summary>
        public event EventHandler? Dismissed;


        /// <summary>
        /// Initialises a new instance of the <see cref="IngestItem"/> class.
        /// </summary>
        /// <param name="work">The work to do when the ingest operation is executed.</param>
        public IngestItem(IngestWork work)
        {
            InitializeComponent();

            task = new IngestTask(work);
            task.PreFileIngested += Task_PreFileIngested;
            task.PostFileIngested += Task_PostFileIngested;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            TextPrompt.Text = string.Format(
                "{0} ({1}:) has {2} file{3} ({4}) in DCIM. Do you want to ingest them?",
                task.Work.VolumeLabel, task.Work.VolumeLetter, task.Work.FilesToIngest.Count,
                GetPluralSuffix(task.Work.FilesToIngest.Count),
                FormatStorageSize(task.Work.TotalIngestSize));
        }

        private void ButtonPromptStart_Click(object sender, RoutedEventArgs e)
        {
            task.DestinationDirectory = Properties.Settings.Default.DestDirectory;
            task.DestinationStructure = (DestStructure)Properties.Settings.Default.DestStructure;
            task.DeleteAfterIngest = Properties.Settings.Default.DeleteAfterIngest!;

            GridPrompt.Visibility = Visibility.Collapsed;
            GridIngest.Visibility = Visibility.Visible;

            Ingest();
        }

        /// <summary>
        /// Executes the ingest operation, making sure the UI is updated appropriately.
        /// </summary>
        private async void Ingest()
        {
            TextIngest1.Text = string.Format("Ingesting {0} file{1} from {2} ({3}:)",
                task.Work.FilesToIngest.Count, GetPluralSuffix(task.Work.FilesToIngest.Count),
                task.Work.VolumeLabel, task.Work.VolumeLetter);

            // Restore UI in case we are retrying after a failure
            ProgressBar1.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            ButtonIngestCancel.Visibility = Visibility.Visible;
            ButtonIngestCancel.IsEnabled = true;
            GridIngestButtons.Visibility = Visibility.Collapsed;
            ButtonIngestRetry.Visibility = Visibility.Collapsed;
            ColDefIngestRetry.Width = new GridLength(0, GridUnitType.Pixel);
            ButtonIngestOpen.Visibility = Visibility.Collapsed;
            ColDefIngestOpen.Width = new GridLength(0, GridUnitType.Pixel);

            try
            {
                await task.Ingest(cancelSource.Token);

                TextIngest1.Text = string.Format("Ingest from {0} ({1}:) completed",
                    task.Work.VolumeLabel, task.Work.VolumeLetter);
            }
            catch (OperationCanceledException)
            {
                TextIngest1.Text = string.Format("Ingest from {0} ({1}:) cancelled",
                    task.Work.VolumeLabel, task.Work.VolumeLetter);
                ProgressBar1.Foreground = new SolidColorBrush(Color.FromRgb(255, 140, 0));
            }
            catch
            {
                TextIngest1.Text = string.Format("Ingest from {0} ({1}:) failed",
                    task.Work.VolumeLabel, task.Work.VolumeLetter);
                ProgressBar1.Foreground = new SolidColorBrush(Color.FromRgb(209, 52, 56));
                ButtonIngestRetry.Visibility = Visibility.Visible;
                ColDefIngestRetry.Width = new GridLength(1, GridUnitType.Star);
            }
            finally
            {
                TextIngest2.Text = string.Format("Transferred {0} file{1}",
                    ingestedCount, GetPluralSuffix(ingestedCount));
            }

            ButtonIngestCancel.Visibility = Visibility.Collapsed;
            GridIngestButtons.Visibility = Visibility.Visible;

            if (firstIngestDir != null)
            {
                ButtonIngestOpen.Visibility = Visibility.Visible;
                ColDefIngestOpen.Width = new GridLength(1, GridUnitType.Star);
            }
        }

        private void Task_PreFileIngested(object? sender, PreFileIngestedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                TextIngest2.Text = string.Format("Transferring {0}",
                    Path.GetFileName(task.Work.FilesToIngest.ElementAt(e.FileIndex)));
            });
        }

        private void Task_PostFileIngested(object? sender, PostFileIngestedEventArgs e)
        {
            ingestedCount++;

            if (firstIngestDir == null)
                firstIngestDir = Path.GetDirectoryName(e.NewFilePath);

            double percentage = ((double)(e.FileIndex + 1) / task.Work.FilesToIngest.Count) * 100;

            Application.Current.Dispatcher.Invoke(() =>
            {
                TextIngestPercent.Text = string.Format("{0}%", Math.Round(percentage));
                ProgressBar1.Value = percentage;
            });
        }

        private void ButtonIngestCancel_Click(object sender, RoutedEventArgs e)
        {
            if (!cancelSource.IsCancellationRequested)
            {
                ButtonIngestCancel.Content = "Cancelling...";
                ButtonIngestCancel.IsEnabled = false;
                cancelSource.Cancel();
            }
        }

        private void ButtonIngestRetry_Click(object sender, RoutedEventArgs e)
        {
            Ingest();
        }

        private void ButtonIngestOpen_Click(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo psi = new(firstIngestDir!)
            {
                Verb = "open",
                UseShellExecute = true
            };

            try
            {
                Process.Start(psi);
            }
            catch { }
        }

        private void ButtonDismiss_Click(object sender, RoutedEventArgs e)
        {
            Dismissed?.Invoke(this, new EventArgs());
        }
    }
}
