using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace GoogleDrive
{
    class FileTransferPage:ContentPage
    {
        CloudFileExplorePanel PNcloud;
        Grid GDmain,GDcloudPanel;
        StackLayout SPbuttons;
        Button BTNuploadFile,BTNuploadFolder, BTNdownload,BTNtest;
        Label LBselected;
        CloudFile fileSelected = null;
        public FileTransferPage()
        {
            InitializeControls();
            RegisterEvents();
            DoAsyncInitializationTasks();
        }
        const string UploadInfoSaveFilePath = "FileTransfer/UploadInfoSaveFile.txt";
        async void DoAsyncInitializationTasks()
        {
            {
                string content = await MyLogger.ReadFileAsync(UploadInfoSaveFilePath);
                if(!string.IsNullOrEmpty(content))
                {
                    var data = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    MyLogger.Assert(data.Length == 2);//file path, resumable uri
                    if(await MyLogger.Ask("Unfinished uploads found, resume?"))
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        throw new NotImplementedException();
                        //if (await MyLogger.Ask(""))
                    }
                }
            }
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
                                var uploadedFolder =await fileSelected.UploadFolderOnWindowsAsync(folder);
                                if (uploadedFolder == null)
                                {
                                    await MyLogger.Alert("Folder not uploaded!");
                                }
                                else
                                {
                                    MyLogger.Log($"Upload successfully completed!\r\nFolder ID: {uploadedFolder.Id}");
                                }
                            }
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
                                var stream = await file.OpenStreamForReadAsync();
                                var uploadedFile = fileSelected.UploadFileAsync(stream, file.Name);
                                if (uploadedFile == null)
                                {
                                    MyLogger.Log("Upload canceled");
                                }
                                else
                                {
                                    MyLogger.Log($"Upload successfully completed!\r\nFile ID: {uploadedFile.Id}");
                                }
                            }
                        }
                        break;
                    default:
                        await MyLogger.Alert($"File picker currently not supported on {Device.RuntimePlatform} devices.");
                        break;
                }
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
                BTNuploadFile.IsEnabled = false;
                await UploadFile(false);
                BTNuploadFile.IsEnabled = true;
            };
            BTNuploadFolder.Clicked += async delegate
            {
                BTNuploadFolder.IsEnabled = false;
                await UploadFile(true);
                BTNuploadFolder.IsEnabled = true;
            };
            BTNdownload.Clicked += async delegate
            {
                await MyLogger.Alert("Not implemented!");
            };
            BTNtest.Clicked += async delegate
            {
                BTNtest.IsEnabled = false;
                if (await MyLogger.Ask("Test now?"))
                {
                    await MyLogger.Alert("Not implemented!");
                }
                BTNtest.IsEnabled = true;
            };
        }
        void InitializeControls()
        {
            this.Title = "File Transfer";
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
                    Grid.SetColumn(GDcloudPanel, 0);
                    GDmain.Children.Add(GDcloudPanel);
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
                        BTNtest = new Button { Text = "Test" };
                        SPbuttons.Children.Add(BTNtest);
                    }
                    Grid.SetColumn(SPbuttons, 1);
                    GDmain.Children.Add(SPbuttons);
                }
                this.Content = GDmain;
            }
        }
    }
}
