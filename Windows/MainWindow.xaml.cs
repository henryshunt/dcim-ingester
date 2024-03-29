﻿using DcimIngester.Controls;
using DcimIngester.Ingesting;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using static DcimIngester.Ingesting.IngestTask;
using static DcimIngester.Utilities;

namespace DcimIngester.Windows
{
    public partial class MainWindow : Window
    {
        private const double WINDOW_MARGIN_X = 16;
        private const double WINDOW_MARGIN_Y = 13;
        private const uint MESSAGE_ID = 0x0401;

        /// <summary>
        /// The identifier returned from registering for volume change notifications.
        /// </summary>
        private uint notifyId = 0;

        /// <summary>
        /// Stores received volume change notifications. Items contain volume letter and change type.
        /// </summary>
        private readonly BlockingCollection<(char, VolumeChangeType)> volumeNotifQueue = [];

        /// <summary>
        /// Thread for handling received volume change notifications.
        /// </summary>
        private Thread? volumeNotifThread = null;

        /// <summary>
        /// Used to cancel the blocking TryTake call used to wait for items in <see cref="volumeNotifQueue"/>.
        /// </summary>
        private readonly CancellationTokenSource queueTakeCancel = new();

        /// <summary>
        /// Gets the number of <see cref="IngestItem"/>s that are currently actively ingesting.
        /// </summary>
        public int ActiveIngestCount
        {
            get => itemsStackPanel.Children.OfType<IngestItem>().Count(
                i => i.Status == IngestTaskStatus.Ingesting);
        }


        /// <summary>
        /// Initialises a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            bool shutdown = true;
            IntPtr windowHandle = new WindowInteropHelper(this).Handle;

            if (HideWindowFromAltTab(windowHandle))
            {
                NativeMethods.SHChangeNotifyEntry entry = new()
                {
                    fRecursive = false
                };

                if (NativeMethods.SHGetKnownFolderIDList(NativeMethods.FOLDERID_DESKTOP, 0, IntPtr.Zero, out entry.pIdl) == 0)
                {
                    // Should be SHCNRF according to docs but for some reason none of those values work
                    // These values are from examples and I have no idea what they mean or do here
                    int sources = NativeMethods.SHCNF_TYPE | NativeMethods.SHCNF_IDLIST;

                    int events = NativeMethods.SHCNE_DRIVEADD | NativeMethods.SHCNE_DRIVEREMOVED |
                        NativeMethods.SHCNE_MEDIAINSERTED | NativeMethods.SHCNE_MEDIAREMOVED;

                    // Register for notifications that indicate when volumes have been added or removed
                    notifyId = NativeMethods.SHChangeNotifyRegister(windowHandle, sources, events, MESSAGE_ID, 1, ref entry);

                    if (notifyId > 0)
                    {
                        volumeNotifThread = new Thread(VolumeNotifThread);
                        volumeNotifThread.Start();

                        HwndSource.FromHwnd(windowHandle).AddHook(WndProc);
                        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
                        shutdown = false;
                    }
                }
            }

            if (shutdown)
                ((App)Application.Current).Shutdown();
        }

        private void Window_Closing(object sender, EventArgs e)
        {
            if (notifyId > 0)
            {
                HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).RemoveHook(WndProc);
                queueTakeCancel.Cancel();
                NativeMethods.SHChangeNotifyDeregister(notifyId);
                SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
            }
        }

        /// <summary>
        /// Invoked when the window receives a message. Reacts to messages that indicate that the volumes on the system
        /// have changed.
        /// </summary>
        private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == MESSAGE_ID)
            {
                NativeMethods.SHNotifyStruct notif = (NativeMethods.SHNotifyStruct)
                    Marshal.PtrToStructure(wParam, typeof(NativeMethods.SHNotifyStruct))!;

                int @event = (int)lParam;

                if (@event == NativeMethods.SHCNE_DRIVEADD || @event == NativeMethods.SHCNE_DRIVEREMOVED ||
                    @event == NativeMethods.SHCNE_MEDIAINSERTED || @event == NativeMethods.SHCNE_MEDIAREMOVED)
                {
                    StringBuilder path = new(3);

                    // Get the path of the volume that changed (expecting "X:/")
                    if (NativeMethods.SHGetPathFromIDListW(notif.dwItem1, path) && path.Length == 3)
                    {
                        if (@event == NativeMethods.SHCNE_DRIVEADD || @event == NativeMethods.SHCNE_MEDIAINSERTED)
                            volumeNotifQueue.Add((path.ToString()[0], VolumeChangeType.Addition));
                        else if (@event == NativeMethods.SHCNE_DRIVEREMOVED || @event == NativeMethods.SHCNE_MEDIAREMOVED)
                            volumeNotifQueue.Add((path.ToString()[0], VolumeChangeType.Removal));
                    }
                }
            }

            handled = false;
            return IntPtr.Zero;
        }

        private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                Left = SystemParameters.WorkArea.Right - Width - WINDOW_MARGIN_X;
                Top = SystemParameters.WorkArea.Bottom - Height - WINDOW_MARGIN_Y;

                // WorkArea does not properly account for the taskbar at the time this event is
                // fired, so need to reposition again after a short wait. Also, don't await because
                // this method should return quickly.
                Task.Delay(500).ContinueWith(t =>
                {
                    Left = SystemParameters.WorkArea.Right - Width - WINDOW_MARGIN_X;
                    Top = SystemParameters.WorkArea.Bottom - Height - WINDOW_MARGIN_Y;
                });
            }
        }

        /// <summary>
        /// Handles queued volume addition and removal notifications. Runs in a separate thread.
        /// </summary>
        private void VolumeNotifThread()
        {
            try
            {
                // In tuple, char is volume letter

                while (volumeNotifQueue.TryTake(out (char, VolumeChangeType) notification,
                    Timeout.Infinite, queueTakeCancel.Token))
                {
                    if (notification.Item2 == VolumeChangeType.Removal)
                        OnVolumeRemoved(notification.Item1);
                    else OnVolumeAdded(notification.Item1);
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// Invoked when a volume addition notification is handled.
        /// </summary>
        /// <param name="volumeLetter">The letter of the volume.</param>
        private void OnVolumeAdded(char volumeLetter)
        {
            bool discover = true;

            Application.Current.Dispatcher.Invoke(() =>
            {
                IngestItem? item = itemsStackPanel.Children.OfType<IngestItem>()
                    .SingleOrDefault(i => i.VolumeLetter == volumeLetter);

                // Not cancelling first because it should have failed if in progress.
                // But just in case it hasn't, don't continue
                if (item != null)
                {
                    if (item.Status != IngestTaskStatus.Ingesting)
                        RemoveItem(item);
                    else discover = false;
                }
            });

            if (discover)
            {
                try
                {
                    IngestWork work = new(volumeLetter);

                    if (work.DiscoverFiles())
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            IngestItem item = new(work);
                            if (itemsStackPanel.Children.Count > 0)
                                item.Margin = new Thickness(0, 12, 0, 0);
                            item.Dismissed += IngestItem_Dismissed;

                            itemsStackPanel.Children.Add(item);

                            Left = SystemParameters.WorkArea.Right - Width - WINDOW_MARGIN_X;
                            Height = itemsStackPanel.Children.OfType<IngestItem>().Sum(i => i.Height + i.Margin.Top);
                            Top = SystemParameters.WorkArea.Bottom - Height - WINDOW_MARGIN_Y;

                            Show();
                        });
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Invoked when a volume removal notification is handled.
        /// </summary>
        /// <param name="volumeLetter">The letter of the volume.</param>
        private void OnVolumeRemoved(char volumeLetter)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Only remove items that have not been started
                IngestItem? item = itemsStackPanel.Children.OfType<IngestItem>().SingleOrDefault(
                    i => i.VolumeLetter == volumeLetter && i.Status == IngestTaskStatus.Ready);

                if (item != null)
                    RemoveItem(item);
            });
        }

        /// <summary>
        /// Removes an <see cref="IngestItem"/> from the UI and hides the window if no items are left.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        private void RemoveItem(IngestItem item)
        {
            itemsStackPanel.Children.Remove(item);

            if (itemsStackPanel.Children.Count > 0)
                ((IngestItem)itemsStackPanel.Children[0]).Margin = new Thickness(0);

            Left = SystemParameters.WorkArea.Right - Width - WINDOW_MARGIN_X;
            Height = itemsStackPanel.Children.OfType<IngestItem>().Sum(i => i.Height + i.Margin.Top);
            Top = SystemParameters.WorkArea.Bottom - Height - WINDOW_MARGIN_Y;

            if (itemsStackPanel.Children.Count == 0)
                Hide();
        }

        private void IngestItem_Dismissed(object? sender, EventArgs e)
        {
            // Need to check because it may be possible for a volume removal notification to remove the
            // item being dismissed between the dismiss button being clicked and this code executing
            if (itemsStackPanel.Children.Contains((IngestItem)sender!))
                RemoveItem((IngestItem)sender!);
        }

        /// <summary>
        /// Specifies whether a volume change event was for addition or removal.
        /// </summary>
        private enum VolumeChangeType { Addition, Removal }
    }
}
