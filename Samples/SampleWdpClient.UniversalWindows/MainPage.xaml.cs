using Microsoft.Tools.WindowsDevicePortal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Cryptography.Certificates;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using static Microsoft.Tools.WindowsDevicePortal.DevicePortal;

namespace SampleWdpClient.UniversalWindows
{
    /// <summary>
    /// The main page of the application.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// The device portal to which we are connecting.
        /// </summary>
        private DevicePortal portal;
        private Certificate certificate;
        private StorageFolder UploadSourceFolder;

        /// <summary>
        /// The main page constructor.
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();
            this.EnableDeviceControls(false);
            this.address.Text = "https://";
        }

        /// <summary>
        /// TextChanged handler for the address text box.
        /// </summary>
        /// <param name="sender">The caller of this method.</param>
        /// <param name="e">The arguments associated with this event.</param>
        private void Address_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableConnectButton();
        }

        /// <summary>
        /// If specified in the UI, clears the test output display, otherwise does nothing.
        /// </summary>
        private void ClearOutput()
        {
            bool clearOutput = this.clearOutput.IsChecked.HasValue ? this.clearOutput.IsChecked.Value : false;
            if (clearOutput)
            {
                this.commandOutput.Text = string.Empty;
            }
        }

        /// <summary>
        /// Click handler for the connectToDevice button.
        /// </summary>
        /// <param name="sender">The caller of this method.</param>
        /// <param name="e">The arguments associated with this event.</param>
        private async void ConnectToDevice_Click(object sender, RoutedEventArgs e)
        {
            this.EnableConnectionControls(false);
            this.EnableDeviceControls(false);

            this.ClearOutput();

            //bool allowUntrusted = this.allowUntrustedCheckbox.IsChecked.Value;
            bool allowUntrusted = true;

            portal = new DevicePortal(
                new DefaultDevicePortalConnection(
                    this.address.Text,
                    this.username.Text,
                    this.password.Password));

            StringBuilder sb = new StringBuilder();

            sb.Append(this.commandOutput.Text);
            sb.AppendLine("Connecting...");
            this.commandOutput.Text = sb.ToString();
            portal.ConnectionStatus += (portal, connectArgs) =>
            {
                if (connectArgs.Status == DeviceConnectionStatus.Connected)
                {
                    sb.Append("Connected to: ");
                    sb.AppendLine(portal.Address);
                    sb.Append("OS version: ");
                    sb.AppendLine(portal.OperatingSystemVersion);
                    sb.Append("Device family: ");
                    sb.AppendLine(portal.DeviceFamily);
                    sb.Append("Platform: ");
                    sb.AppendLine(String.Format("{0} ({1})",
                        portal.PlatformName,
                        portal.Platform.ToString()));
                }
                else if (connectArgs.Status == DeviceConnectionStatus.Failed)
                {
                    sb.AppendLine("Failed to connect to the device.");
                    sb.AppendLine(connectArgs.Message);
                }
            };

            try
            {
                // If the user wants to allow untrusted connections, make a call to GetRootDeviceCertificate
                // with acceptUntrustedCerts set to true. This will enable untrusted connections for the
                // remainder of this session.
                if (allowUntrusted)
                {
                    this.certificate = await portal.GetRootDeviceCertificateAsync(true);
                }
                await portal.ConnectAsync(manualCertificate: this.certificate);
            }
            catch (Exception exception)
            {
                sb.AppendLine(exception.Message);
            }

            this.commandOutput.Text = sb.ToString();
            EnableDeviceControls(true);
            EnableConnectionControls(true);
        }

        /// <summary>
        /// Enables or disables the Connect button based on the current state of the
        /// Address, User name and Password fields.
        /// </summary>
        private void EnableConnectButton()
        {
            bool enable = (!string.IsNullOrWhiteSpace(this.address.Text) &&
                        !string.IsNullOrWhiteSpace(this.username.Text) &&
                        !string.IsNullOrWhiteSpace(this.password.Password));

            this.connectToDevice.IsEnabled = enable;
        }

        /// <summary>
        /// Sets the IsEnabled property appropriately for the connection controls.
        /// </summary>
        /// <param name="enable">True to enable the controls, false to disable them.</param>
        private void EnableConnectionControls(bool enable)
        {
            this.address.IsEnabled = enable;
            this.username.IsEnabled = enable;
            this.password.IsEnabled = enable;

            this.connectToDevice.IsEnabled = enable;
        }

        /// <summary>
        /// Sets the IsEnabled property appropriately for the device command controls.
        /// </summary>
        /// <param name="enable">True to enable the controls, false to disable them.</param>
        private void EnableDeviceControls(bool enable)
        {
            this.rebootDevice.IsEnabled = enable;
            this.shutdownDevice.IsEnabled = enable;
            this.ClearScans.IsEnabled = enable;
            this.getDirectory.IsEnabled = enable;
            this.UploadScans.IsEnabled = enable && this.UploadSourceFolder != null;
        }

        public async Task ClearSurgeryDirectory()
        {
            FolderContents subFolders = await portal.GetFolderContentsAsync("Pictures", "Surgery");
            foreach (var item in subFolders.Contents)
            {
                if (item.IsFolder)
                {
                    string subPath = "Surgery/" + item.Name;
                    FolderContents images = await portal.GetFolderContentsAsync("Pictures", subPath);
                    foreach (var image in images.Contents)
                    {
                        await portal.DeleteFileAsync("Pictures", image.Name, subPath);
                    }
                }
            }
        }

        public Task UploadFile(string filename, string targetDirectory)
        {
            string targetFolder = "Surgery/" + targetDirectory;
            Task uploadT = new Task(
                async () =>
                {
                    await portal.UploadFileAsync("Pictures", filename, targetFolder);
                });
            uploadT.Start();
            return uploadT;
        }

        /// <summary>
        /// Click handler for the getIpConfig button.
        /// </summary>
        /// <param name="sender">The caller of this method.</param>
        /// <param name="e">The arguments associated with this event.</param>
        private async void UploadScans_Click(object sender, RoutedEventArgs e)
        {
            this.ClearOutput();
            this.EnableConnectionControls(false);
            this.EnableDeviceControls(false);

            StringBuilder sb = new StringBuilder();
            sb.Append(commandOutput.Text);
            sb.AppendLine("Uploading Files...");
            commandOutput.Text = sb.ToString();
    
            try
            {
                var subFolders = await UploadSourceFolder.GetFoldersAsync();
                var hololensSurgeryDirs = await portal.GetFolderContentsAsync("Pictures", "Surgery");
                var dirNames = hololensSurgeryDirs.Contents
                    .Where(item => item.IsFolder)
                    .Select(folder => folder.Name)
                    .ToList();

                foreach (StorageFolder subFolder in subFolders)
                {
                    string targetDir = subFolder.Name;
                    if (!dirNames.Contains(targetDir))
                    {
                        sb.AppendLine("Folder " + targetDir + " not found on hololens, skipping...");
                        continue;
                    }
                    var images = await subFolder.GetFilesAsync();
                    foreach (StorageFile image in images)
                    {
                        var goodFileTypes = new List<string>(){ "jpg", "jpeg", "png" };
                        var imageNameSplit = image.Name.Split('.');
                        if (!goodFileTypes.Contains(imageNameSplit.Last()))
                        {
                            sb.AppendLine("File " + image.Name + " does not have correct file type, skipping...");
                            continue;
                        }
                        string filePath = image.Path;
                        await UploadFile(filePath, targetDir);
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("Failed");
                sb.AppendLine(ex.GetType().ToString() + " - " + ex.Message);
            }

            sb.AppendLine("Done uploading Files...");

            commandOutput.Text = sb.ToString();
            EnableDeviceControls(true);
            EnableConnectionControls(true);
        }


        /// <summary>
        /// PasswordChanged handler for the password text box.
        /// </summary>
        /// <param name="sender">The caller of this method.</param>
        /// <param name="e">The arguments associated with this event.</param>
        private void Password_PasswordChanged(object sender, RoutedEventArgs e)
        {
            EnableConnectButton();
        }

        /// <summary>
        /// Click handler for the rebootDevice button.
        /// </summary>
        /// <param name="sender">The caller of this method.</param>
        /// <param name="e">The arguments associated with this event.</param>
        private async void RebootDevice_Click(object sender, RoutedEventArgs e)
        {
            bool reenableDeviceControls = false;

            this.ClearOutput();
            this.EnableConnectionControls(false);
            this.EnableDeviceControls(false);

            StringBuilder sb = new StringBuilder();

            sb.Append(commandOutput.Text);
            sb.AppendLine("Rebooting the device");
            commandOutput.Text = sb.ToString();

            try
            {
                await portal.RebootAsync();
            }
            catch (Exception ex)
            {
                sb.AppendLine("Failed to reboot the device.");
                sb.AppendLine(ex.GetType().ToString() + " - " + ex.Message);
                reenableDeviceControls = true;
            }

            commandOutput.Text = sb.ToString();
            EnableDeviceControls(reenableDeviceControls);
            EnableConnectionControls(true);
        }

        /// <summary>
        /// Click handler for the shutdownDevice button.
        /// </summary>
        /// <param name="sender">The caller of this method.</param>
        /// <param name="e">The arguments associated with this event.</param>
        private async void ShutdownDevice_Click(object sender, RoutedEventArgs e)
        {
            bool reenableDeviceControls = false;

            this.ClearOutput();
            this.EnableConnectionControls(false);
            this.EnableDeviceControls(false);

            StringBuilder sb = new StringBuilder();
            sb.Append(commandOutput.Text);
            sb.AppendLine("Shutting down the device");
            commandOutput.Text = sb.ToString();
            try
            {
                await portal.ShutdownAsync();
            }
            catch (Exception ex)
            {
                sb.AppendLine("Failed to shut down the device.");
                sb.AppendLine(ex.GetType().ToString() + " - " + ex.Message);
                reenableDeviceControls = true;
            }

            commandOutput.Text = sb.ToString();
            EnableDeviceControls(reenableDeviceControls);
            EnableConnectionControls(true);
        }

        /// <summary>
        /// TextChanged handler for the username text box.
        /// </summary>
        /// <param name="sender">The caller of this method.</param>
        /// <param name="e">The arguments associated with this event.</param>
        private void Username_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableConnectButton();
        }

        /// <summary>
        /// Loads a cert file for cert validation.
        /// </summary>
        /// <param name="sender">The caller of this method.</param>
        /// <param name="e">The arguments associated with this event.</param>
        private async void GetDirectory_Click(object sender, RoutedEventArgs e)
        {
            await GetUploadSourceFolder();
            EnableDeviceControls(true);
        }

        private async Task GetUploadSourceFolder()
        {
            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");

            UploadSourceFolder = await folderPicker.PickSingleFolderAsync();
            if (UploadSourceFolder != null)
            {
                // Application now has read/write access to all contents in the picked folder
                // (including other sub-folder contents)
                Windows.Storage.AccessCache.StorageApplicationPermissions.
                FutureAccessList.AddOrReplace("PickedFolderToken", UploadSourceFolder);
                commandOutput.Text = UploadSourceFolder.Name;
            }
        }


        private async void ClearScans_Click(object sender, RoutedEventArgs e)
        {
            this.ClearOutput();
            this.EnableConnectionControls(false);
            this.EnableDeviceControls(false);

            StringBuilder sb = new StringBuilder();

            sb.Append(commandOutput.Text);
            sb.AppendLine("Clearing surgery directory on Hololens...");
            commandOutput.Text = sb.ToString();

            await ClearSurgeryDirectory();

            sb.AppendLine("Done clearing surgery directory");

            commandOutput.Text = sb.ToString();
            EnableDeviceControls(true);
            EnableConnectionControls(true);
        }

        
    }
}