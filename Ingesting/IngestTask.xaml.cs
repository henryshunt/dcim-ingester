﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace DcimIngester.Ingesting
{
    public partial class IngestTask : UserControl
    {
        public IngestTaskContext Context { get; private set; }
        public IngestTaskStatus Status { get; private set; } = IngestTaskStatus.Prompting;

        private Thread ingestThread;
        private bool shouldCancelIngest = false;
        private int lastFileIngested = -1;
        private string firstFileDestination = null;
        private int duplicateCounter = 0;
        private int unsortedCounter = 0;

        public event EventHandler Dismissed;

        public IngestTask(IngestTaskContext context)
        {
            Context = context;
            InitializeComponent();
        }

        public void Load() => Prompt();

        private void Prompt()
        {
            if (Context.VolumeLabel == "")
            {
                LabelPromptCaption.Text =
                    string.Format("Unnamed volume contains {0} files ({1}). Do you want to ingest them?",
                    Context.FilesToIngest.Count, Utilities.FormatBytes(Context.TotalIngestSize));
            }
            else
            {
                LabelPromptCaption.Text =
                    string.Format("Volume '{0}' contains {1} files ({2}). Do you want to ingest them?",
                    Context.VolumeLabel, Context.FilesToIngest.Count, Utilities.FormatBytes(Context.TotalIngestSize));
            }

            CheckBoxPromptDelete.IsChecked = Properties.Settings.Default.ShouldDeleteAfter;
        }
        private void ButtonPromptYes_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ShouldDeleteAfter = (bool)CheckBoxPromptDelete.IsChecked;
            Properties.Settings.Default.Save();

            Ingest();
        }
        private void ButtonPromptNo_Click(object sender, RoutedEventArgs e)
        {
            Dismissed?.Invoke(this, new EventArgs());
        }

        private void Ingest()
        {
            Status = IngestTaskStatus.Ingesting;
            GridPrompt.Visibility = Visibility.Collapsed;
            GridIngest.Visibility = Visibility.Visible;

            ingestThread = new Thread(IngestFiles);
            ingestThread.Start();
        }
        private void ButtonIngestCancel_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonIngestCancel.Content.ToString() == "Cancel")
            {
                ButtonIngestCancel.IsEnabled = false;
                ButtonIngestCancel.Content = "Cancelling";
                shouldCancelIngest = true;
            }
            else if (ButtonIngestCancel.Content.ToString() == "Dismiss")
                Dismissed?.Invoke(this, new EventArgs());
        }
        private void ButtonIngestRetry_Click(object sender, RoutedEventArgs e)
        {
            ButtonIngestCancel.Content = "Cancel";
            ButtonIngestCancel.IsEnabled = true;
            shouldCancelIngest = false;

            ButtonIngestRetry.Visibility = Visibility.Collapsed;
            ButtonIngestOpen.Visibility = Visibility.Collapsed;

            Ingest();
        }
        private void ButtonIngestOpen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open the destination directory of the first file transferred
                Process.Start(new ProcessStartInfo(firstFileDestination) { UseShellExecute = true, Verb = "open" });
            }
            catch { }
        }

        private void IngestFiles()
        {
            for (int i = lastFileIngested + 1; i < Context.FilesToIngest.Count; i++)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Context.VolumeLabel == "")
                    {
                        LabelIngestCaption.Text = string.Format("Transferring file {0} of {1} from unnamed volume",
                            i + 1, Context.FilesToIngest.Count);
                    }
                    else
                    {
                        LabelIngestCaption.Text = string.Format("Transferring file {0} of {1} from '{2}'",
                            i + 1, Context.FilesToIngest.Count, Context.VolumeLabel);
                    }

                    double percentage = ((double)i / Context.FilesToIngest.Count) * 100;
                    LabelIngestPercentage.Content = string.Format("{0}%", Math.Round(percentage));
                    ProgressBarIngest.Value = percentage;

                    LabelIngestSubCaption.Text = string.Format(
                        "{0} renamed duplicates, {1} ingested to 'Unsorted' folder", duplicateCounter, unsortedCounter);
                });

                if (!IngestFile(Context.FilesToIngest.ElementAt(i)))
                {
                    Application.Current.Dispatcher.Invoke(IngestFailed);
                    return;
                }

                lastFileIngested++;

                if (shouldCancelIngest)
                {
                    // Only cancel if the file we just ingested was not the final file
                    if (lastFileIngested < Context.FilesToIngest.Count - 1)
                    {
                        Application.Current.Dispatcher.Invoke(IngestCancelled);
                        return;
                    }
                }
            }

            Application.Current.Dispatcher.Invoke(IngestCompleted);
        }
        private bool IngestFile(string path)
        {
            DateTime? dateTaken;
            try
            {
                dateTaken = Utilities.GetDateTaken(path);
            }
            catch { return false; }

            string destination;
            bool isUnsorted = false;

            // Determine destination based on date taken and selected subfolder template
            if (dateTaken != null)
            {
                switch (Properties.Settings.Default.Subfolders)
                {
                    case 0:
                        {
                            destination = Path.Combine(Properties.Settings.Default.Destination, string.Format(
                                "{0:D4}\\{1:D2}\\{2:D2}", dateTaken?.Year, dateTaken?.Month, dateTaken?.Day));
                            break;
                        }
                    case 1:
                        {
                            destination = Path.Combine(Properties.Settings.Default.Destination, string.Format(
                                "{0:D4}\\{0:D4}-{1:D2}-{2:D2}", dateTaken?.Year, dateTaken?.Month, dateTaken?.Day));
                            break;
                        }
                    case 2:
                        {
                            destination = Path.Combine(Properties.Settings.Default.Destination, string.Format(
                                "{0:D4}-{1:D2}-{2:D2}", dateTaken?.Year, dateTaken?.Month, dateTaken?.Day));
                            break;
                        }
                    default: return false;
                }
            }
            else
            {
                isUnsorted = true;
                destination = Path.Combine(Properties.Settings.Default.Destination, "Unsorted");
            }

            try
            {
                destination = Utilities.CreateIngestDirectory(destination);
                Utilities.CopyFile(path, destination, out bool isRenamed);

                if (isRenamed)
                    duplicateCounter++;
                if (isUnsorted)
                    unsortedCounter++;

                if (firstFileDestination == null)
                    firstFileDestination = destination;

                if (Properties.Settings.Default.ShouldDeleteAfter)
                    File.Delete(path);

                return true;
            }
            catch { return false; }
        }
        private void IngestCompleted()
        {
            Status = IngestTaskStatus.Completed;

            if (Context.VolumeLabel == "")
                LabelIngestCaption.Text = string.Format("Ingest from unnamed volume complete");
            else LabelIngestCaption.Text = string.Format("Ingest from '{0}' complete", Context.VolumeLabel);

            LabelIngestPercentage.Content = "100%";
            ProgressBarIngest.Value = 100;

            LabelIngestSubCaption.Text = string.Format(
                "{0} renamed duplicates, {1} ingested to 'Unsorted' folder", duplicateCounter, unsortedCounter);

            ButtonIngestCancel.Content = "Dismiss";
            ButtonIngestOpen.Visibility = Visibility.Visible;
        }
        private void IngestFailed()
        {
            Status = IngestTaskStatus.Failed;

            if (Context.VolumeLabel == "")
                LabelIngestCaption.Text = string.Format("Ingest from unnamed volume failed");
            else LabelIngestCaption.Text = string.Format("Ingest from '{0}' failed", Context.VolumeLabel);

            ButtonIngestCancel.Content = "Dismiss";
            ButtonIngestRetry.Visibility = Visibility.Visible;

            if (lastFileIngested > 0)
                ButtonIngestOpen.Visibility = Visibility.Visible;
        }
        private void IngestCancelled()
        {
            Status = IngestTaskStatus.Cancelled;

            if (Context.VolumeLabel == "")
                LabelIngestCaption.Text = string.Format("Ingest from unnamed volume cancelled");
            else LabelIngestCaption.Text = string.Format("Ingest from '{0}' cancelled", Context.VolumeLabel);

            ButtonIngestCancel.Content = "Dismiss";
            ButtonIngestRetry.Visibility = Visibility.Visible;

            if (lastFileIngested > 0)
                ButtonIngestOpen.Visibility = Visibility.Visible;
        }
    }
}
