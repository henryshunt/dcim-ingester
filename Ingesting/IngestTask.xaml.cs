﻿using DCIMIngester.Routines;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace DCIMIngester.Ingesting
{
    public partial class IngestTask : UserControl
    {
        public IngestTaskContext Context { get; private set; }
        public TaskStatus Status { get; set; } = TaskStatus.Prompting;

        private Thread IngestThread;
        private bool ShouldCancelIngest = false;
        private int LastFileIngested = -1;
        private string FirstFileDestination = null;
        private int DuplicateCounter = 0;
        private int UnsortedCounter = 0;

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
                    Context.FilesToIngest.Count, Helpers.FormatBytes(Context.TotalIngestSize));
            }
            else
            {
                LabelPromptCaption.Text =
                    string.Format("Volume '{0}' contains {1} files ({2}). Do you want to ingest them?",
                    Context.VolumeLabel, Context.FilesToIngest.Count, Helpers.FormatBytes(Context.TotalIngestSize));
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
            Status = TaskStatus.Ingesting;
            GridPrompt.Visibility = Visibility.Collapsed;
            GridIngest.Visibility = Visibility.Visible;

            IngestThread = new Thread(IngestFiles);
            IngestThread.Start();
        }
        private void ButtonIngestCancel_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonIngestCancel.Content.ToString() == "Cancel")
            {
                ButtonIngestCancel.IsEnabled = false;
                ButtonIngestCancel.Content = "Cancelling";
                ShouldCancelIngest = true;
            }
            else if (ButtonIngestCancel.Content.ToString() == "Dismiss")
                Dismissed?.Invoke(this, new EventArgs());
        }
        private void ButtonIngestRetry_Click(object sender, RoutedEventArgs e)
        {
            ButtonIngestCancel.Content = "Cancel";
            ButtonIngestCancel.IsEnabled = true;
            ShouldCancelIngest = false;

            ButtonIngestRetry.Visibility = Visibility.Collapsed;
            ButtonIngestView.Visibility = Visibility.Collapsed;

            Ingest();
        }
        private void ButtonIngestView_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open the destination directory of the first file transferred
                Process.Start(new ProcessStartInfo(FirstFileDestination) { UseShellExecute = true, Verb = "open" });
            }
            catch { }
        }

        private void IngestFiles()
        {
            for (int i = LastFileIngested + 1; i < Context.FilesToIngest.Count; i++)
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
                        "{0} renamed duplicates, {1} ingested to 'Unsorted' folder", DuplicateCounter, UnsortedCounter);
                });

                if (!IngestFile(Context.FilesToIngest.ElementAt(i)))
                {
                    Application.Current.Dispatcher.Invoke(IngestFailed);
                    return;
                }

                LastFileIngested++;

                if (ShouldCancelIngest)
                {
                    // Only cancel if the file we just ingested was not the final file
                    if (LastFileIngested < Context.FilesToIngest.Count - 1)
                    {
                        Application.Current.Dispatcher.Invoke(IngestCancelled);
                        return;
                    }
                }
            }

            Application.Current.Dispatcher.Invoke(IngestCompleted);
        }
        private bool IngestFile(string filePath)
        {
            try
            {
                DateTime? timeTaken = Helpers.GetTimeTaken(filePath);

                string destinationDir;
                bool isUnsorted = false;

                // Determine destination based on time taken and selected subfolder organisation
                if (timeTaken != null)
                {
                    switch (Properties.Settings.Default.Subfolders)
                    {
                        case 0:
                            {
                                destinationDir = Path.Combine(Properties.Settings.Default.Endpoint, string.Format(
                                    "{0:D4}\\{1:D2}\\{2:D2}", timeTaken?.Year, timeTaken?.Month, timeTaken?.Day));
                                break;
                            }
                        case 1:
                            {
                                destinationDir = Path.Combine(Properties.Settings.Default.Endpoint, string.Format(
                                    "{0:D4}\\{0:D4}-{1:D2}-{2:D2}", timeTaken?.Year, timeTaken?.Month, timeTaken?.Day));
                                break;
                            }
                        case 2:
                            {
                                destinationDir = Path.Combine(Properties.Settings.Default.Endpoint, string.Format(
                                    "{0:D4}-{1:D2}-{2:D2}", timeTaken?.Year, timeTaken?.Month, timeTaken?.Day));
                                break;
                            }
                        default: return false;
                    }
                }
                else
                {
                    isUnsorted = true;
                    destinationDir = Path.Combine(Properties.Settings.Default.Endpoint, "Unsorted");
                }

                destinationDir = Helpers.CreateDirectory(destinationDir);

                if (Helpers.CopyFile(filePath, destinationDir))
                    DuplicateCounter++;

                if (isUnsorted)
                    UnsortedCounter++;

                if (FirstFileDestination == null)
                    FirstFileDestination = destinationDir;

                if (Properties.Settings.Default.ShouldDeleteAfter)
                    File.Delete(filePath);
            }
            catch { return false; }

            return true;
        }
        private void IngestCompleted()
        {
            Status = TaskStatus.Completed;

            if (Context.VolumeLabel == "")
                LabelIngestCaption.Text = string.Format("Ingest from unnamed volume complete");
            else LabelIngestCaption.Text = string.Format("Ingest from '{0}' complete", Context.VolumeLabel);

            LabelIngestPercentage.Content = "100%";
            ProgressBarIngest.Value = 100;

            LabelIngestSubCaption.Text = string.Format(
                "{0} renamed duplicates, {1} ingested to 'Unsorted' folder", DuplicateCounter, UnsortedCounter);

            ButtonIngestCancel.Content = "Dismiss";
            ButtonIngestView.Visibility = Visibility.Visible;
        }
        private void IngestFailed()
        {
            Status = TaskStatus.Failed;

            if (Context.VolumeLabel == "")
                LabelIngestCaption.Text = string.Format("Ingest from unnamed volume failed");
            else LabelIngestCaption.Text = string.Format("Ingest from '{0}' failed", Context.VolumeLabel);

            ButtonIngestCancel.Content = "Dismiss";
            ButtonIngestRetry.Visibility = Visibility.Visible;

            if (LastFileIngested > 0)
                ButtonIngestView.Visibility = Visibility.Visible;
        }
        private void IngestCancelled()
        {
            Status = TaskStatus.Cancelled;

            if (Context.VolumeLabel == "")
                LabelIngestCaption.Text = string.Format("Ingest from unnamed volume cancelled");
            else LabelIngestCaption.Text = string.Format("Ingest from '{0}' cancelled", Context.VolumeLabel);

            ButtonIngestCancel.Content = "Dismiss";
            ButtonIngestRetry.Visibility = Visibility.Visible;

            if (LastFileIngested > 0)
                ButtonIngestView.Visibility = Visibility.Visible;
        }

        public enum TaskStatus { Prompting, Ingesting, Completed, Failed, Cancelled };
    }
}