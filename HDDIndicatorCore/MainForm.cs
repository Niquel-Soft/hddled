using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HDDIndicatorCore
{
    public partial class MainForm : Form
    {
        NotifyIcon hddIndicatorIcon;
        Icon activeIcon;
        Icon idleIcon;
        CancellationTokenSource cts;
        Task hddInfoWorkerTask;

        public MainForm()
        {
            InitializeComponent();
            try
            {
                activeIcon = new Icon("HDD_Busy.ico");
                idleIcon = new Icon("HDD_Idle.ico");
            }
            catch
            {
                // Fallback to a system icon if resource files are missing
                activeIcon = SystemIcons.Application;
                idleIcon = SystemIcons.Application;
            }
            hddIndicatorIcon = new NotifyIcon
            {
                Icon = idleIcon,
                Visible = true
            };

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            var progNameMenuItem = contextMenu.Items.Add("HDD Usage Indicator v1.0.2");
            var quitMenuItem = contextMenu.Items.Add("Quit");
            hddIndicatorIcon.ContextMenuStrip = contextMenu;

            quitMenuItem.Click += QuitMenuItem_Click;

            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;

            //this.Controls.Add(contextMenu);

            // Start worker task, which pulls HDD activity. Use cancellation token for clean shutdown.
            cts = new CancellationTokenSource();
            hddInfoWorkerTask = Task.Run(() => HddActivityLoop(cts.Token), cts.Token);
        }

        private async void QuitMenuItem_Click(object sender, EventArgs e)
        {
            // Request cancellation and wait briefly for the task to complete
            try
            {
                cts?.Cancel();
                if (hddInfoWorkerTask != null)
                {
                    await hddInfoWorkerTask.ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException) { }
            catch { }

            hddIndicatorIcon.Dispose();
            this.Close();
        }

        private async Task HddActivityLoop(CancellationToken token)
        {
            ManagementClass driveDataClass = new ManagementClass("Win32_PerfFormattedData_PerfDisk_PhysicalDisk");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        ManagementObjectCollection driveDataClassCollection = driveDataClass.GetInstances();
                        foreach (ManagementObject obj in driveDataClassCollection)
                        {
                            if (obj["Name"].ToString() == "_Total")
                            {
                                bool busy = false;
                                try
                                {
                                    busy = Convert.ToUInt64(obj["DiskBytesPersec"]) > 0;
                                }
                                catch { }

                                // Marshal icon update to UI thread
                                if (hddIndicatorIcon != null)
                                {
                                    if (this.InvokeRequired)
                                    {
                                        this.BeginInvoke((Action)(() => hddIndicatorIcon.Icon = busy ? activeIcon : idleIcon));
                                    }
                                    else
                                    {
                                        hddIndicatorIcon.Icon = busy ? activeIcon : idleIcon;
                                    }
                                }
                            }
                        }
                    }
                    catch (ManagementException)
                    {
                        // Ignore WMI transient errors
                    }

                    try
                    {
                        await Task.Delay(100, token).ConfigureAwait(true);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
            finally
            {
                driveDataClass.Dispose();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                cts?.Cancel();
                if (hddInfoWorkerTask != null)
                {
                    hddInfoWorkerTask.Wait(500);
                }
            }
            catch { }

            hddIndicatorIcon?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
