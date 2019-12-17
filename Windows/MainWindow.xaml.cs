﻿using DCIMIngester.Routines;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using static DCIMIngester.Routines.Helpers;
using static DCIMIngester.Routines.VolumeWatcher;

namespace DCIMIngester.Windows
{
    public partial class MainWindow : Window
    {
        private VolumeWatcher Volumes = new VolumeWatcher();
        private List<IngesterTask> Tasks = new List<IngesterTask>();

        public MainWindow()
        {
            InitializeComponent();
        }


        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);

            // Register for device change messages
            if (source != null)
            {
                Volumes.VolumeAdded += Devices_VolumeAdded;
                Volumes.VolumeRemoved += Devices_VolumeRemoved;
                Volumes.StartWatching(source);
            }
        }
        private void Devices_VolumeAdded(object sender, VolumeChangedEventArgs e)
        {
            if (!IsLoaded) return;

            string volumeLetter = GetVolumeLetter(e.VolumeID);
            if (Directory.Exists(Path.Combine(volumeLetter, "DCIM"))
                && HasFilesToTransfer(volumeLetter))
            {
                StartIngesterTask(e.VolumeID);
            }
        }
        private void Devices_VolumeRemoved(object sender, VolumeChangedEventArgs e)
        {
            if (!IsLoaded) return;
            StopIngesterTask(e.VolumeID, true);
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            Volumes.StopWatching();
        }

        private void StartIngesterTask(Guid volume)
        {
            Height += 140;
            Rect workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 20;
            Top = workArea.Bottom - Height - 20;
            Show();

            IngesterTask task = new IngesterTask(volume);
            task.TaskDismissed += Task_TaskDismissed;

            Tasks.Add(task);
            StackPanelTasks.Children.Add(task);
            task.Start();
        }
        private void Task_TaskDismissed(object sender, TaskDismissEventArgs e)
        {
            StopIngesterTask(e.Task.Volume);
        }
        private void StopIngesterTask(Guid volume, bool onlyIfWaiting = false)
        {
            foreach (IngesterTask task in Tasks)
            {
                if (task.Volume == volume)
                {
                    // Option to only remove task if in waiting state
                    if (onlyIfWaiting && task.Status != TaskStatus.Waiting)
                        return;

                    task.Stop();
                    StackPanelTasks.Children.Remove(task);
                    Tasks.Remove(task);

                    Height -= 140;
                    Rect workArea = SystemParameters.WorkArea;
                    Left = workArea.Right - Width - 20;
                    Top = workArea.Bottom - Height - 20;

                    if (Tasks.Count == 0) Hide();
                    break;
                }
            }
        }
    }
}