/* Copyright (C) 2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ISCSI.Server;
using DiskAccessLibrary;
using Utilities;

namespace ISCSIConsole
{
    public partial class MainForm : Form
    {
        private ISCSIServer m_server = new ISCSIServer();
        private List<ISCSITarget> m_targets = new List<ISCSITarget>();
        private UsageCounter m_usageCounter = new UsageCounter();
        private bool m_started = false;
        private bool m_restoringConfiguration = false;
        private bool m_autoStartLaunch = false;

        public MainForm() : this(false)
        {
        }

        public MainForm(bool autoStartLaunch)
        {
            m_autoStartLaunch = autoStartLaunch;
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Text += " v" + version.ToString(3) + " AutoStart";
            m_server.OnLogEntry += Program.OnLogEntry;

            List<IPAddress> localIPs = GetHostIPAddresses();
            KeyValuePairList<string, IPAddress> list = new KeyValuePairList<string, IPAddress>();
            list.Add("Any", IPAddress.Any);
            foreach (IPAddress address in localIPs)
            {
                list.Add(address.ToString(), address);
            }
            comboIPAddress.DataSource = list;
            comboIPAddress.DisplayMember = "Key";
            comboIPAddress.ValueMember = "Value";
            lblStatus.Text = "Author: Tal Aloni (AutoStart build)";

            if (RuntimeHelper.IsWin32 && !SecurityHelper.IsAdministrator())
            {
                lblStatus.Text = "Run as administrator once to register Windows startup";
            }

            RestoreConfiguration();

            if (m_autoStartLaunch)
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    this.WindowState = FormWindowState.Minimized;
                });
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (!m_started)
            {
                if (StartServer())
                {
                    SaveConfiguration(true);
                    RegisterWindowsStartup();
                }
            }
            else
            {
                m_server.Stop();
                lblStatus.Text = String.Empty;
                m_started = false;
                btnStart.Text = "Start";
                txtPort.Enabled = true;
                comboIPAddress.Enabled = true;
                SaveConfiguration(false);
            }
        }

        private bool StartServer()
        {
            IPAddress serverAddress = (IPAddress)comboIPAddress.SelectedValue;
            int port = Conversion.ToInt32(txtPort.Text, 0);
            if (port <= 0 || port > UInt16.MaxValue)
            {
                MessageBox.Show("Invalid TCP port", "Error");
                return false;
            }

            IPEndPoint endpoint = new IPEndPoint(serverAddress, port);
            try
            {
                m_server.Start(endpoint);
            }
            catch (SocketException ex)
            {
                MessageBox.Show("Cannot start server, " + ex.Message, "Error");
                return false;
            }

            btnStart.Text = "Stop";
            txtPort.Enabled = false;
            comboIPAddress.Enabled = false;
            m_started = true;
            UpdateUI();
            return true;
        }

        private void btnAddTarget_Click(object sender, EventArgs e)
        {
            AddTargetForm addTarget = new AddTargetForm();
            DialogResult addTargetResult = addTarget.ShowDialog();
            if (addTargetResult == DialogResult.OK)
            {
                ISCSITarget target = addTarget.Target;
                if (RegisterTarget(target))
                {
                    SaveConfiguration(m_started);
                }
            }
        }

        private bool RegisterTarget(ISCSITarget target)
        {
            ((SCSI.VirtualSCSITarget)target.SCSITarget).OnLogEntry += Program.OnLogEntry;
            target.OnAuthorizationRequest += new EventHandler<AuthorizationRequestArgs>(ISCSITarget_OnAuthorizationRequest);
            target.OnSessionTermination += new EventHandler<SessionTerminationArgs>(ISCSITarget_OnSessionTermination);

            try
            {
                m_server.AddTarget(target);
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(ex.Message, "Error");
                return false;
            }

            m_targets.Add(target);
            listTargets.Items.Add(target.TargetName);
            return true;
        }

        private void btnRemoveTarget_Click(object sender, EventArgs e)
        {
            if (listTargets.SelectedIndices.Count > 0)
            {
                int targetIndex = listTargets.SelectedIndices[0];
                ISCSITarget target = m_targets[targetIndex];
                bool isTargetRemoved = m_server.RemoveTarget(target.TargetName);
                if (!isTargetRemoved)
                {
                    MessageBox.Show("Could not remove iSCSI target", "Error");
                    return;
                }
                List<Disk> disks = ((SCSI.VirtualSCSITarget)target.SCSITarget).Disks;
                LockUtils.ReleaseDisks(disks);
                m_targets.RemoveAt(targetIndex);
                listTargets.Items.RemoveAt(targetIndex);
                SaveConfiguration(m_started);
            }
        }

        private void RestoreConfiguration()
        {
            AutoStartConfiguration configuration;
            try
            {
                configuration = AutoStartConfigurationStore.Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot load AutoStart configuration: " + ex.Message, "AutoStart");
                return;
            }

            if (configuration == null)
            {
                return;
            }

            m_restoringConfiguration = true;
            try
            {
                txtPort.Text = configuration.Port.ToString();

                IPAddress configuredAddress;
                if (IPAddress.TryParse(configuration.IPAddress, out configuredAddress))
                {
                    bool addressAvailable = configuredAddress.Equals(IPAddress.Any);
                    if (!addressAvailable)
                    {
                        foreach (IPAddress localAddress in GetHostIPAddresses())
                        {
                            if (localAddress.Equals(configuredAddress))
                            {
                                addressAvailable = true;
                                break;
                            }
                        }
                    }

                    if (addressAvailable)
                    {
                        comboIPAddress.SelectedValue = configuredAddress;
                    }
                    else
                    {
                        comboIPAddress.SelectedValue = IPAddress.Any;
                        lblStatus.Text = "Saved IP is unavailable; listening on Any address";
                    }
                }

                if (configuration.Targets != null)
                {
                    foreach (TargetConfiguration targetConfiguration in configuration.Targets)
                    {
                        RestoreTarget(targetConfiguration);
                    }
                }

                if (configuration.StartServerOnLaunch && m_targets.Count > 0)
                {
                    StartServer();
                }
            }
            finally
            {
                m_restoringConfiguration = false;
            }
        }

        private void RestoreTarget(TargetConfiguration targetConfiguration)
        {
            List<Disk> disks = new List<Disk>();
            try
            {
                if (targetConfiguration.DiskImagePaths == null || targetConfiguration.DiskImagePaths.Count == 0)
                {
                    return;
                }

                foreach (string path in targetConfiguration.DiskImagePaths)
                {
                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundException("Disk image not found", path);
                    }

                    DiskImage diskImage = DiskImage.GetDiskImage(path, false);
                    bool isLocked = false;
                    try
                    {
                        isLocked = diskImage.ExclusiveLock();
                    }
                    catch (IOException)
                    {
                    }

                    if (!isLocked)
                    {
                        throw new IOException("Cannot lock disk image for exclusive access: " + path);
                    }
                    disks.Add(diskImage);
                }

                ISCSITarget target = new ISCSITarget(targetConfiguration.IQN, disks);
                if (!RegisterTarget(target))
                {
                    LockUtils.ReleaseDisks(disks);
                }
            }
            catch (Exception ex)
            {
                LockUtils.ReleaseDisks(disks);
                MessageBox.Show("Cannot restore target " + targetConfiguration.IQN + ": " + ex.Message, "AutoStart");
            }
        }

        private void SaveConfiguration(bool startServerOnLaunch)
        {
            if (m_restoringConfiguration)
            {
                return;
            }

            AutoStartConfiguration configuration = new AutoStartConfiguration();
            configuration.StartServerOnLaunch = startServerOnLaunch;
            IPAddress selectedAddress = comboIPAddress.SelectedValue as IPAddress;
            configuration.IPAddress = selectedAddress == null ? IPAddress.Any.ToString() : selectedAddress.ToString();
            configuration.Port = Conversion.ToInt32(txtPort.Text, 3260);

            foreach (ISCSITarget target in m_targets)
            {
                TargetConfiguration targetConfiguration = new TargetConfiguration();
                targetConfiguration.IQN = target.TargetName;

                List<Disk> disks = ((SCSI.VirtualSCSITarget)target.SCSITarget).Disks;
                bool supported = true;
                foreach (Disk disk in disks)
                {
                    DiskImage diskImage = disk as DiskImage;
                    if (diskImage == null)
                    {
                        supported = false;
                        break;
                    }
                    targetConfiguration.DiskImagePaths.Add(diskImage.Path);
                }

                if (supported && targetConfiguration.DiskImagePaths.Count > 0)
                {
                    configuration.Targets.Add(targetConfiguration);
                }
            }

            try
            {
                AutoStartConfigurationStore.Save(configuration);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot save AutoStart configuration: " + ex.Message, "AutoStart");
            }
        }

        private void RegisterWindowsStartup()
        {
            if (!RuntimeHelper.IsWin32)
            {
                return;
            }

            string error;
            if (!StartupTaskManager.EnsureInstalled(out error))
            {
                lblStatus.Text = "iSCSI started, but Windows startup registration failed";
                MessageBox.Show(
                    "The server is running, but Windows startup registration failed.\r\n" +
                    "Run iSCSIConsole as administrator and click Start again.\r\n\r\n" + error,
                    "AutoStart");
            }
        }

        private void ISCSITarget_OnAuthorizationRequest(object sender, AuthorizationRequestArgs e)
        {
            string targetName = ((ISCSITarget)sender).TargetName;
            m_usageCounter.NotifySessionStart(targetName);
            UpdateUI();
        }

        private void ISCSITarget_OnSessionTermination(object sender, SessionTerminationArgs e)
        {
            string targetName = ((ISCSITarget)sender).TargetName;
            m_usageCounter.NotifySessionTermination(targetName);
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)UpdateUI);
            }
            else
            {
                if (m_started)
                {
                    lblStatus.Text = String.Format("{0} Active Sessions", m_usageCounter.SessionCount);
                }

                if (listTargets.SelectedIndices.Count > 0)
                {
                    int targetIndex = listTargets.SelectedIndices[0];
                    ISCSITarget target = m_targets[targetIndex];
                    bool isInUse = m_usageCounter.IsTargetInUse(target.TargetName);
                    btnRemoveTarget.Enabled = !isInUse;
                }
                else
                {
                    btnRemoveTarget.Enabled = false;
                }
            }
        }

        private static List<IPAddress> GetHostIPAddresses()
        {
            List<IPAddress> result = new List<IPAddress>();
            foreach (NetworkInterface netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                IPInterfaceProperties ipProperties = netInterface.GetIPProperties();
                foreach (UnicastIPAddressInformation addressInfo in ipProperties.UnicastAddresses)
                {
                    if (addressInfo.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        result.Add(addressInfo.Address);
                    }
                }
            }
            return result;
        }

        private void listTargets_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateUI();
        }
    }
}
