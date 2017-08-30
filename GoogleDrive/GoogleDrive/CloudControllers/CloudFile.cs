using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace GoogleDrive
{
    public delegate void MessageAppendedEventHandler(string msg);

    partial class CloudFile
    {
        //public class Verifiers
        //{
        //    public class FileVerifier : Networker
        //    {
        //        public FileVerifier(CloudFile cloudFile, Windows.Storage.StorageFile windowsFile)
        //        {
        //        }
        //    }
        //}
        #region Fields
        public string Id { get; private set; }
        public string Name { get; private set; }
        public bool IsFolder { get; private set; }
        private CloudFile parent;
        public string FullName
        {
            get
            {
                if (parent == null) return "/";
                else return $"{(parent.FullName)}{Name}{(IsFolder ? "/" : "")}";
            }
        }
        #endregion
        #region InnerMethods
        //private async Task<Tuple<ulong, ulong, ulong>> CountFilesAndFoldersRecursivelyOnWindowsAsync(Windows.Storage.StorageFolder folder)
        //{
        //    ulong contentLength = 0, fileCount = 0, folderCount = 1;
        //    var storageFiles = await folder.GetFilesAsync();
        //    fileCount += (ulong)storageFiles.Count;
        //    foreach (var storageFile in storageFiles) contentLength += (await storageFile.GetBasicPropertiesAsync()).Size;
        //    foreach (var storageFolder in await folder.GetFoldersAsync())
        //    {
        //        var result = await CountFilesAndFoldersRecursivelyOnWindowsAsync(storageFolder);
        //        contentLength += result.Item1;
        //        fileCount += result.Item2;
        //        folderCount += result.Item3;
        //    }
        //    return new Tuple<ulong, ulong, ulong>(contentLength, fileCount, folderCount);
        //}
        //private async Task<CloudFile> CreateFolderRecursivelyOnWindowsAsync(Windows.Storage.StorageFolder folder)
        //{
        //    MyLogger.Assert(this.IsFolder);
        //    MyLogger.Log($"Creating folder: {folder.Name} ({this.FullName})");
        //    var cloudFolder = await this.CreateFolderAsync(folder.Name);
        //    foreach (var storageFolder in await folder.GetFoldersAsync())
        //    {
        //        await cloudFolder.CreateFolderRecursivelyOnWindowsAsync(storageFolder);
        //    }
        //    return cloudFolder;
        //}
        //private async Task UploadFileRecursivelyOnWindowsAsync(Windows.Storage.StorageFolder folder)
        //{
        //    MyLogger.Assert(this.IsFolder);
        //    MyLogger.Assert(this.Name == folder.Name);
        //    foreach (var storageFile in await folder.GetFilesAsync())
        //    {
        //        MyLogger.Log($"Uploading file: {storageFile.Name} ({this.FullName})");
        //        await this.UploadFileAsync(storageFile);
        //    }
        //    foreach (var storageFolder in await folder.GetFoldersAsync())
        //    {
        //        var cloudFolder = await this.GetFolderAsync(storageFolder.Name);
        //        MyLogger.Assert(cloudFolder != null);
        //        await cloudFolder.UploadFileRecursivelyOnWindowsAsync(storageFolder);
        //    }
        //}
        private async Task<CloudFile> CreateEmptyFileAsync(string fileName)
        {
            MyLogger.Assert(this.IsFolder);
            var request = (await Drive.GetDriveServiceAsync()).Files.Create(
                new Google.Apis.Drive.v3.Data.File
                {
                    Name = fileName,
                    Parents = new List<string> { this.Id },
                    MimeType = Constants.GetMimeType(System.IO.Path.GetExtension(fileName))
                });
            MyLogger.Log($"Creating empty file... {fileName} ({this.FullName})");
            var result = await new Func<Task<Google.Apis.Drive.v3.Data.File>, Task<Google.Apis.Drive.v3.Data.File>>((Task<Google.Apis.Drive.v3.Data.File> task) =>
            {
                task.ContinueWith(t =>
                {
                    // NotOnRanToCompletion - this code will be called if the upload fails
                    MyLogger.Log($"Failed to create file:\r\n{t.Exception}");
                }, TaskContinuationOptions.NotOnRanToCompletion);
                task.ContinueWith(t =>
                {
                    MyLogger.Log($"File created successfully: {fileName} ({this.FullName})");
                });
                return task;
            })(request.ExecuteAsync());
            MyLogger.Assert(result.Name == fileName);
            var ans = new CloudFile(result.Id, result.Name, false, this);
            return ans;
        }
        private SearchListGetter FilesGetter(bool isFolder)
        {
            MyLogger.Assert(this.IsFolder);
            return FilesGetter($"'{this.Id}' in parents and trashed != true and mimeType {(isFolder ? "=" : "!=")} '{Constants.FolderMimeType}'");
        }
        private SearchListGetter FilesGetter(string pattern)
        {
            MyLogger.Assert(this.IsFolder);
            return new SearchListGetter(this, pattern);
        }
        #endregion
        #region PublicMethods
        public async Task<Windows.Storage.StorageFolder> DownloadFolderOnWindowsAsync(Windows.Storage.StorageFolder localDestinationFolder)
        {
            await Task.Delay(1000);
            throw new NotImplementedException();
        }
        public async Task DownloadFileOnWindowsAsync(Windows.Storage.StorageFile file)
        {
            try
            {
                MyLogger.Assert(!this.IsFolder);
                MyLogger.Assert(this.Name == file.Name);
                var downloader = new CloudFile.Downloaders.FileDownloader(this, file);
                downloader.MessageAppended += new MessageAppendedEventHandler((msg) =>
                    {
                        MyLogger.Log(msg);
                    });
                downloader.StatusChanged += new Networker.NetworkStatusChangedEventHandler(async () =>
                  {
                      if (downloader.Status == Networker.NetworkStatus.Completed)
                      {
                          MyLogger.Log($"File download succeeded!\r\nName: {file.Name}\r\nPath: {file.Path}\r\nID: {this.Id}\r\nSize: {(await file.GetBasicPropertiesAsync()).Size} bytes");
                      }
                  });
                await downloader.StartAsync();
            }
            catch (Exception error)
            {
                MyLogger.Log(error.ToString());
                await MyLogger.Alert(error.ToString());
            }
        }
        public async Task<CloudFile> UploadFolderOnWindowsAsync(Windows.Storage.StorageFolder folder)
        {
            MyLogger.Assert(this.IsFolder);
            if (await this.GetFolderAsync(folder.Name) != null)
            {
                if (!await MyLogger.Ask($"Folder, \"{folder.Name}\", already existed in \"{this.FullName}\"!\r\nStill want to upload?")) return null;
            }
            var t = new CloudFile.Uploaders.FolderUploader(this, folder);
            await t.StartAsync();
            return t.UploadedCloudFolder;
            /*old code
            {
                MyLogger.Log("Counting total size, files and folders...");
                var statistic = await CountFilesAndFoldersRecursivelyOnWindowsAsync(folder);
                MyLogger.Log($"{statistic.Item1} bytes, {statistic.Item2} files, {statistic.Item3} folders to upload");
                CloudFile cloudFolderToUpload;
                {
                    ulong cnt = 0;
                    MyLogger.SetStatus1("Creating folders");
                    MyLogger.SetProgress1(0.0);
                    var folderCreatedEvent = new FolderCreatedEventHandler((lambda_folder) =>
                    {
                        cnt++;
                        double progress = (double)cnt / statistic.Item3;
                        MyLogger.SetStatus1($"Creating folders...{(progress * 100).ToString("F3")}% ({cnt}/{statistic.Item3})");
                        MyLogger.SetProgress1(progress);
                    });
                    FolderCreated += folderCreatedEvent;
                    try
                    {
                        cloudFolderToUpload = await CreateFolderRecursivelyOnWindowsAsync(folder);
                    }
                    catch (Exception error)
                    {
                        MyLogger.Log($"Error when creating folders:\r\n{error}");
                        return null;
                    }
                    FolderCreated -= folderCreatedEvent;
                }
                {
                    ulong cntWeight = 1024;
                    var setTotalProgress = new Action<ulong, ulong>((lambda_fileCount, lambda_uploadedSize) =>
                       {
                           double progress = (double)(lambda_uploadedSize + cntWeight * lambda_fileCount) / (statistic.Item1 + cntWeight * statistic.Item2);
                           MyLogger.SetStatus1($"Uploading files...{(progress * 100).ToString("F3")}% ({lambda_fileCount}/{statistic.Item2} files) ({lambda_uploadedSize}/{statistic.Item1} bytes)");
                           MyLogger.SetProgress1(progress);
                       });
                    ulong fileCount = 0, uploadedSize = 0;
                    MyLogger.SetStatus1("Uploading files");
                    MyLogger.SetProgress1(0.0);
                    var fileUploadedEvent = new FileUploadedEventHandler((lambda_file, lambda_fileSize) =>
                      {
                          fileCount++;
                          uploadedSize += lambda_fileSize;
                          setTotalProgress(fileCount, uploadedSize);
                      });
                    var fileUploadProgressChangedEvent = new FileUploadProgressChangedEventHandler((lambda_fileName, lambda_bytesSent, lambda_totalLength) =>
                      {
                          setTotalProgress(fileCount, uploadedSize + (ulong)lambda_bytesSent);
                      });
                    FileUploaded += fileUploadedEvent;
                    FileUploadProgressChanged += fileUploadProgressChangedEvent;
                    try
                    {
                        await cloudFolderToUpload.UploadFileRecursivelyOnWindowsAsync(folder);
                        MyLogger.Log($"Folder upload succeeded! {uploadedSize}/{statistic.Item1} bytes, {fileCount}/{statistic.Item2} files, {statistic.Item3} folders");
                    }
                    catch (Exception error)
                    {
                        MyLogger.Log($"Error when uploading files:\r\n{error}");
                    }
                    FileUploadProgressChanged -= fileUploadProgressChangedEvent;
                    FileUploaded -= fileUploadedEvent;
                }
                return cloudFolderToUpload;
            }
            */
        }
        public async Task<CloudFile> UploadFileAsync(Windows.Storage.StorageFile file)
        {
            try
            {
                var fileSize = (await file.GetBasicPropertiesAsync()).Size;
                if (fileSize == 0)
                {
                    var uploadedFile = await CreateEmptyFileAsync(file.Name);
                    MyLogger.Log($"File upload succeeded!\r\nName: {uploadedFile.Name}\r\nParent: {this.FullName}\r\nID: {uploadedFile.Id}\r\nSize: {fileSize} bytes");
                    MyLogger.Assert(uploadedFile.Name == file.Name);
                    return uploadedFile;
                }
                MyLogger.Assert(this.IsFolder);
                var uploader = new Uploaders.FileUploader(this, file, file.Name);
                indexRetry:;
                await uploader.StartAsync();
                switch (uploader.Status)
                {
                    case Networker.NetworkStatus.Completed:
                        {
                            MyLogger.Log($"File upload succeeded!\r\nName: {file.Name}\r\nParent: {this.FullName}\r\nID: {uploader.UploadedCloudFile.Id}\r\nSize: {fileSize} bytes");
                            var ans = new CloudFile(uploader.UploadedCloudFile.Id, file.Name, false, this);
                            return ans;
                        }
                    case Networker.NetworkStatus.Paused:
                        {
                            MyLogger.Log("Upload paused");
                            return null;
                        }
                    default:
                        {
                            if (await MyLogger.Ask("Upload failed, try again?"))
                            {
                                await uploader.StartAsync();
                                goto indexRetry;
                            }
                            else
                            {
                                MyLogger.Log("Upload canceled");
                                return null;
                            }
                        }
                }
            }
            catch (Exception error)
            {
                MyLogger.Log(error.ToString());
                await MyLogger.Alert(error.ToString());
                return null;
            }
        }
        public async Task<CloudFile> CreateFolderAsync(string folderName)
        {
            MyLogger.Assert(this.IsFolder);
            var folderCreator = new RestRequests.FileCreator(this.Id, folderName, true);
            //var request = (await Drive.GetDriveServiceAsync()).Files.Create(
            //    new Google.Apis.Drive.v3.Data.File
            //    {
            //        Name = folderName,
            //        Parents = new List<string> { this.Id },
            //        MimeType = Constants.FolderMimeType
            //    });
            ////MyLogger.Log($"Creating folder... {folderName} ({this.FullName})");
            //var result = await request.ExecuteAsync();
            ////var result = await new Func<Task<Google.Apis.Drive.v3.Data.File>, Task<Google.Apis.Drive.v3.Data.File>>((Task<Google.Apis.Drive.v3.Data.File> task) =>
            ////{
            ////    task.ContinueWith(t =>
            ////    {
            ////        // NotOnRanToCompletion - this code will be called if the upload fails
            ////        MyLogger.Log($"Failed to create folder:\r\n{t.Exception}");
            ////    }, TaskContinuationOptions.NotOnRanToCompletion);
            ////    task.ContinueWith(t =>
            ////    {
            ////        MyLogger.Log($"Folder created successfully: {folderName} ({this.FullName})");
            ////    });
            ////    return task;
            ////})();
            //MyLogger.Assert(result.Name == folderName);
            await folderCreator.Start();
            MyLogger.Assert(folderCreator.Result != null);
            var ans = new CloudFile(folderCreator.Result, folderName, true, this);
            return ans;
        }
        public SearchListGetter FoldersGetter()
        {
            MyLogger.Assert(this.IsFolder);
            return FilesGetter(true);
        }
        public SearchListGetter FilesGetter()
        {
            MyLogger.Assert(this.IsFolder);
            return FilesGetter(false);
        }
        public async Task<CloudFile> GetFolderAsync(string folderName, bool assertSingleFolder = true)
        {
            MyLogger.Assert(this.IsFolder);
            var ans = await FilesGetter($"'{this.Id}' in parents and trashed != true and mimeType = '{Constants.FolderMimeType}' and name = '{folderName}'").GetNextPageAsync(2);
            if (ans.Count == 0) return null;
            if (assertSingleFolder) MyLogger.Assert(ans.Count == 1);
            return ans[0];
        }
        #endregion
        public static CloudFile RootFolder { get { return new CloudFile("root", null, true, null); } }
        private static void Log(string log) { MyLogger.Log(log); }
        public CloudFile(string id, string name, bool isFolder, CloudFile _parent) { Id = id; Name = name; IsFolder = isFolder; parent = _parent; }
    }
}