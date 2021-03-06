﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Xamarin.Forms;
using GoogleDrive.MyControls;
using System.Linq;

namespace GoogleDrive.Pages
{
    class FileBrowsePage:MyContentPage
    {
        CloudFileExplorePanel PNcloud;
        Grid GDmain,GDcloudPanel;
        StackLayout SPbuttons;
        Button BTNuploadFile,BTNuploadFolder, BTNdownload,BTNverify;
        Label LBselected;
        CloudFile fileSelected = null;
        EventHandler initializeThis;
        public FileBrowsePage():base("File Browse")
        {
            this.Appearing += (initializeThis = new EventHandler(delegate
              {
                  this.Appearing -= initializeThis;
                  MyLogger.AddTestMethod("Create file", new Func<Task>(async () =>
                  {
                      var fileCreator = new RestRequests.FileCreator(fileSelected.Id, "Hi", false);
                      fileCreator.MessageAppended += (log) => { MyLogger.Log(log); };
                      await fileCreator.Start();
                      await MyLogger.Alert(fileCreator.Result);
                  }));
                  InitializeControls();
                  RegisterEvents();
              }));
        }
        private async Task UploadFile(bool isFolder)
        {
            if (fileSelected == null)
            {
                await MyLogger.Alert("Please select a cloud folder first");
            }
            else if (!fileSelected.IsFolder)
            {
                await MyLogger.Alert("Please select a \"Cloud Folder\" instead of a \"Cloud File\"");
            }
            else
            {
                switch (Device.RuntimePlatform)
                {
                    case Device.Windows:
                        if (isFolder)
                        {
                            var picker = new Windows.Storage.Pickers.FolderPicker()
                            {
                                ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
                                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
                            };
                            picker.FileTypeFilter.Clear();
                            picker.FileTypeFilter.Add("*");
                            var folder = await picker.PickSingleFolderAsync();
                            if (folder != null)
                            {
                                var containingCloudFolder = fileSelected;
                                MyLogger.Log($"Folder uploading...\r\nName: {folder.Name}\r\nIn: {containingCloudFolder.FullName}\r\nLocal: {folder.Path}");
                                var uploadedFolder = await containingCloudFolder.UploadFolderOnWindowsAsync(folder);
                                if (uploadedFolder == null)
                                {
                                    MyLogger.Log($"Folder upload failed!\r\nName: {folder.Name}\r\nIn: {containingCloudFolder.FullName}\r\nLocal: {folder.Path}");
                                }
                                else
                                {
                                    MyLogger.Log($"Folder upload succeeded!\r\nName: {uploadedFolder.Name}\r\nIn: {containingCloudFolder.FullName}\r\nID: {uploadedFolder.Id}\r\nLocal: {folder.Path}");
                                }
                            }
                            MyLogger.Log("All done!");
                        }
                        else
                        {
                            var picker = new Windows.Storage.Pickers.FileOpenPicker()
                            {
                                ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
                                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
                            };
                            picker.FileTypeFilter.Clear();
                            picker.FileTypeFilter.Add("*");
                            var fileList = await picker.PickMultipleFilesAsync();
                            if (fileList != null)
                            {
                                await Task.WhenAll(fileList.Select(async (file) =>
                                {
                                    var containingCloudFolder = fileSelected;
                                    MyLogger.Log($"File uploading...\r\nName: {file.Name}\r\nIn: {containingCloudFolder.FullName}\r\nLocal: {file.Path}");
                                    var uploadedFile = await containingCloudFolder.UploadFileAsync(file);
                                    if (uploadedFile == null)
                                    {
                                        MyLogger.Log($"File upload canceled!\r\nName: {file.Name}\r\nIn: {containingCloudFolder.FullName}\r\nLocal: {file.Path}");
                                    }
                                    else
                                    {
                                        MyLogger.Log($"File upload succeeded!\r\nName: {uploadedFile.Name}\r\nIn: {containingCloudFolder.FullName}\r\nID: {uploadedFile.Id}\r\nLocal: {file.Path}");
                                    }
                                }));
                            }
                            MyLogger.Log("All done!");
                        }
                        break;
                    default:
                        await MyLogger.Alert($"File picker currently not supported on {Device.RuntimePlatform} devices.");
                        break;
                }
            }
        }
        private async Task DownloadFile()
        {
            if (fileSelected == null)
            {
                await MyLogger.Alert("Please select a cloud folder first");
            }
            else
            {
                switch (Device.RuntimePlatform)
                {
                    case Device.Windows:
                        if(fileSelected.IsFolder)
                        {
                            var picker = new Windows.Storage.Pickers.FolderPicker()
                            {
                                ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
                                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
                            };
                            picker.FileTypeFilter.Clear();
                            picker.FileTypeFilter.Add("*");
                            var folder = await picker.PickSingleFolderAsync();
                            if (folder != null)
                            {
                                var folderToDownload = fileSelected;
                                MyLogger.Log($"Folder downloading...\r\nCloud: {folderToDownload.FullName}\r\nLocal: {folder.Path}");
                                var downloadedFolder = await fileSelected.DownloadFolderOnWindowsAsync(folder);
                                if (downloadedFolder == null)
                                {
                                    MyLogger.Log($"Folder download canceled!\r\nCloud: {folderToDownload.FullName}\r\nLocal: {folder.Path}");
                                }
                                else
                                {
                                    MyLogger.Log($"Folder download succeeded!\r\nCloud: {folderToDownload.FullName}\r\nDownloaded: {downloadedFolder.Path}");
                                }
                            }
                            MyLogger.Log("All done!");
                        }
                        else
                        {
                            var picker = new Windows.Storage.Pickers.FolderPicker()
                            {
                                ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
                                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
                            };
                            picker.FileTypeFilter.Clear();
                            picker.FileTypeFilter.Add("*");
                            var folder = await picker.PickSingleFolderAsync();
                            if (folder != null)
                            {
                                var fileToDownload = fileSelected;
                                {
                                    try
                                    {
                                        var existedFile = await folder.GetFileAsync(fileToDownload.Name);
                                        MyLogger.Assert(existedFile != null);
                                        if (await MyLogger.Ask($"\"{fileToDownload.Name}\" already existed in \"{folder.Path}\", overwrite anyway?"))
                                        {
                                            await existedFile.DeleteAsync();
                                        }
                                        else goto indexSkip;
                                    }
                                    catch(FileNotFoundException)
                                    {
                                        MyLogger.Log("File not found exception, YA!");
                                    }
                                }
                                var localFile = await folder.CreateFileAsync(fileToDownload.Name);
                                MyLogger.Log($"File downloading...\r\nCloud: {fileToDownload.FullName}\r\nLocal: {localFile.Path}");
                                await fileSelected.DownloadFileOnWindowsAsync(localFile);
                                indexSkip:;
                            }
                            MyLogger.Log("All done!");
                        }
                        break;
                    default:
                        await MyLogger.Alert($"File picker currently not supported on {Device.RuntimePlatform} devices.");
                        break;
                }
            }
        }
        private async Task VerifyFile()
        {
            if (fileSelected == null)
            {
                await MyLogger.Alert("Cloud File or Folder must be Selected First");
                return;
            }
            switch (Device.RuntimePlatform)
            {
                case Device.Windows:
                    if (fileSelected.IsFolder)
                    {
                        var picker = new Windows.Storage.Pickers.FolderPicker()
                        {
                            ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
                            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
                        };
                        picker.FileTypeFilter.Clear();
                        picker.FileTypeFilter.Add("*");
                        var folder = await picker.PickSingleFolderAsync();
                        if (folder != null)
                        {
                            var folderToVerify = fileSelected;
                            MyLogger.Log($"Folder Verifying...\r\nCloud: {folderToVerify.FullName}\r\nLocal: {folder.Path}");
                            var verifier = new CloudFile.Verifiers.FolderVerifier(folderToVerify, folder);
                            await verifier.StartUntilCompletedAsync();
                            var msg = $"Folder Verify succeeded!\r\nCloud: {folderToVerify.FullName}\r\nLocal: {folder.Path}";
                            MyLogger.Log(msg);
                            await MyLogger.Alert(msg);
                        }
                        MyLogger.Log("All done!");
                    }
                    else
                    {
                        var picker = new Windows.Storage.Pickers.FileOpenPicker()
                        {
                            ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
                            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
                        };
                        picker.FileTypeFilter.Clear();
                        picker.FileTypeFilter.Add("*");
                        var file = await picker.PickSingleFileAsync();
                        if (file != null)
                        {
                            var cloudFile = fileSelected;
                            MyLogger.Log($"File Verifying...\r\nCloud: {cloudFile.FullName}\r\nLocal: {file.Path}");
                            var verifier = new CloudFile.Verifiers.FileVerifier(cloudFile, file);
                            await verifier.StartUntilCompletedAsync();
                            var msg = $"File Verify succeeded!\r\nCloud: {cloudFile.FullName}\r\nLocal: {file.Path}";
                            MyLogger.Log(msg);
                            await MyLogger.Alert(msg);
                        }
                        MyLogger.Log("All done!");
                    }
                    break;
                default:
                    await MyLogger.Alert($"File picker currently not supported on {Device.RuntimePlatform} devices.");
                    break;
            }
        }
        void RegisterEvents()
        {
            PNcloud.SelectedFileChanged += delegate (CloudFile file)
            {
                fileSelected = file;
                LBselected.Text = file.FullName;
            };
            BTNuploadFile.Clicked += async delegate
            {
                //BTNuploadFile.IsEnabled = false;
                await UploadFile(false);
                //BTNuploadFile.IsEnabled = true;
            };
            BTNuploadFolder.Clicked += async delegate
            {
                //BTNuploadFolder.IsEnabled = false;
                await UploadFile(true);
                //BTNuploadFolder.IsEnabled = true;
            };
            BTNdownload.Clicked += async delegate
            {
                //BTNdownload.IsEnabled = false;
                await DownloadFile();
                //BTNdownload.IsEnabled = true;
            };
            BTNverify.Clicked += async delegate
            {
                await VerifyFile();
            };
        }
        void InitializeControls()
        {
            {
                GDmain = new Grid();
                GDmain.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                GDmain.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                {
                    GDcloudPanel = new Grid();
                    GDcloudPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                    GDcloudPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    {
                        LBselected = new Label {Text="(No file or folder selected)" };
                        Grid.SetRow(LBselected, 0);
                        GDcloudPanel.Children.Add(LBselected);
                    }
                    {
                        PNcloud = new CloudFileExplorePanel();
                        Grid.SetRow(PNcloud, 1);
                        GDcloudPanel.Children.Add(PNcloud);
                    }
                    GDmain.Children.Add(new Frame { OutlineColor = Color.Accent, Padding = new Thickness(5), Content = GDcloudPanel, BackgroundColor = Color.LightYellow }, 0,0);
                }
                {
                    SPbuttons = new StackLayout { Orientation = StackOrientation.Vertical };
                    {
                        BTNuploadFile = new Button { Text = "Upload File" };
                        SPbuttons.Children.Add(BTNuploadFile);
                    }
                    {
                        BTNuploadFolder = new Button { Text = "Upload Folder" };
                        SPbuttons.Children.Add(BTNuploadFolder);
                    }
                    {
                        BTNdownload = new Button { Text = "Download" };
                        SPbuttons.Children.Add(BTNdownload);
                    }
                    {
                        BTNverify = new Button { Text = "Verify" };
                        SPbuttons.Children.Add(BTNverify);
                    }
                    GDmain.Children.Add(new Frame { OutlineColor = Color.Accent, Padding = new Thickness(5), Content = SPbuttons }, 1,0);
                }
                this.Content = GDmain;
            }
        }
    }
}
