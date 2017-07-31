using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;

namespace GoogleDrive
{
    public delegate void MessageAppendedEventHandler(string msg);
    class CloudFile
    {
        public abstract class Networker
        {
            public enum NetworkStatus { NotStarted, Starting, ErrorNeedRestart, Networking, Paused, Completed };
            public delegate void NetworkStatusChangedEventHandler();
            public delegate void NetworkProgressChangedEventHandler(long now,long total);
            public event MessageAppendedEventHandler MessageAppended;
            public event NetworkStatusChangedEventHandler StatusChanged;
            public event NetworkProgressChangedEventHandler ProgressChanged;
            protected void OnMessageAppended(string msg) { MessageAppended?.Invoke(msg); }
            protected void OnStatusChanged() { StatusChanged?.Invoke(); }
            protected void OnProgressChanged(long now,long total) { ProgressChanged?.Invoke(now, total); }
            NetworkStatus __Status__ = NetworkStatus.NotStarted;
            public NetworkStatus Status
            {
                get
                {
                    return __Status__;
                }
                protected set
                {
                    __Status__ = value;
                    OnStatusChanged();
                }
            }
            public abstract Task ResetAsync();
            public abstract Task PauseAsync();
            public abstract Task StartAsync();
        }
        public class Uploaders
        {
            public class FileUploader:Networker
            {
                public delegate void NewUploadCreatedEventHandler(FileUploader uploader);
                public static event NewUploadCreatedEventHandler NewUploadCreated;
                CloudFile CloudFolder;
                public CloudFile UploadedCloudFile
                {
                    get;
                    private set;
                }
                string fileName;
                public long BytesUploaded { get; private set; }
                public long TotalFileLength { get; private set; }
                public FileUploader(CloudFile _cloudFolder, Windows.Storage.StorageFile _windowsFile,string _fileName)
                {
                    CloudFolder = _cloudFolder;
                    windowsFile = _windowsFile;
                    fileName = _fileName;
                    NewUploadCreated?.Invoke(this);
                }
                Windows.Storage.StorageFile windowsFile = null;
                RestRequests.Uploader uploader = null;
                System.IO.Stream fileStream = null;
                public override async Task ResetAsync()
                {
                    Status = NetworkStatus.NotStarted;
                    fileStream = await windowsFile.OpenStreamForWriteAsync();
                    uploader = new RestRequests.Uploader(new List<string> { CloudFolder.Id }, fileStream, fileName);
                }
                public override async Task PauseAsync()
                {
                    await uploader.PauseAsync();
                }
                public override async Task StartAsync()
                {
                    switch (Status)
                    {
                        case NetworkStatus.ErrorNeedRestart:
                        case NetworkStatus.NotStarted:
                        case NetworkStatus.Paused:
                            {
                                if (Status != NetworkStatus.Paused)
                                {
                                    Status = NetworkStatus.Starting;
                                    //MyLogger.Assert(downloader == null && windowsFile == null && fileStream == null);
                                    fileStream = await windowsFile.OpenStreamForWriteAsync();
                                    uploader = new RestRequests.Uploader(new List<string> { CloudFolder.Id }, fileStream, fileName);
                                }
                                Status = NetworkStatus.Networking;
                                var progressChangedEventHandler = new RestRequests.ProgressChangedEventHandler((bytesProcessed, totalLength) =>
                                {
                                    BytesUploaded = bytesProcessed;
                                    TotalFileLength = totalLength;
                                    MyLogger.Assert(this.GetType() == typeof(Uploaders.FileUploader));
                                    OnProgressChanged(BytesUploaded, TotalFileLength);
                                });
                                var messageAppendedEventHandler = new MessageAppendedEventHandler((msg) =>
                                {
                                    OnMessageAppended("Rest: " + msg);
                                });
                                uploader.ProgressChanged += progressChangedEventHandler;
                                uploader.MessageAppended += messageAppendedEventHandler;
                                await uploader.UploadAsync();
                                uploader.ProgressChanged -= progressChangedEventHandler;
                                uploader.MessageAppended -= messageAppendedEventHandler;
                                uploadAgain_index:;
                                switch (uploader.Status)
                                {
                                    case RestRequests.Uploader.UploadStatus.Completed:
                                        {
                                            UploadedCloudFile = new CloudFile(uploader.CloudFileId, fileName, false, CloudFolder);
                                            Status = NetworkStatus.Completed;
                                            return;
                                        }
                                    case RestRequests.Uploader.UploadStatus.ErrorNeedRestart:
                                        {
                                            OnMessageAppended("Error need restart");
                                            Status = NetworkStatus.ErrorNeedRestart;
                                            return;
                                        }
                                    case RestRequests.Uploader.UploadStatus.ErrorNeedResume:
                                        {
                                            OnMessageAppended("Error need resume...");
                                            goto uploadAgain_index;
                                        }
                                    case RestRequests.Uploader.UploadStatus.Paused:
                                        {
                                            Status = NetworkStatus.Paused;
                                            return;
                                        }
                                    case RestRequests.Uploader.UploadStatus.Uploading:
                                    case RestRequests.Uploader.UploadStatus.NotStarted:
                                    default: throw new Exception($"uploader.Status: {uploader.Status}");
                                }
                            }
                        case NetworkStatus.Completed:
                        case NetworkStatus.Networking:
                        case NetworkStatus.Starting:
                        default: throw new Exception($"Status: {Status}");
                    }
                }
            }
        }
        public class Downloaders
        {
            public class FileDownloader:Networker
            {
                public delegate void NewDownloadCreatedEventHandler(FileDownloader downloader);
                public static event NewDownloadCreatedEventHandler NewDownloadCreated;
                const string CacheFolder = "DownloadFileCache";
                public CloudFile CloudFile
                {
                    get;
                    private set;
                }
                public long BytesDownloaded { get; private set; }
                public long TotalFileLength { get; private set; }
                public FileDownloader(CloudFile _cloudFile,Windows.Storage.StorageFile _windowsFile)
                {
                    CloudFile = _cloudFile;
                    windowsFile = _windowsFile;
                    NewDownloadCreated?.Invoke(this);
                }
                Windows.Storage.StorageFile windowsFile = null;
                RestRequests.Downloader downloader = null;
                System.IO.Stream fileStream = null;
                private async Task<Windows.Storage.StorageFile> CreateTemporaryFile()
                {
                    await Task.Delay(0);
                    return windowsFile;
                    //var temporaryFolder = await Windows.Storage.ApplicationData.Current.LocalCacheFolder.CreateFolderAsync(CacheFolder, Windows.Storage.CreationCollisionOption.OpenIfExists);
                    //return await temporaryFolder.CreateFileAsync(DateTime.Now.Ticks.ToString(), Windows.Storage.CreationCollisionOption.GenerateUniqueName);
                }
                public override async Task ResetAsync()
                {
                    windowsFile = await CreateTemporaryFile();
                    fileStream = await windowsFile.OpenStreamForWriteAsync();
                    downloader = new RestRequests.Downloader(CloudFile.Id, fileStream);
                }
                public override async Task PauseAsync()
                {
                    await downloader.PauseAsync();
                }
                public override async Task StartAsync()
                {
                    switch (Status)
                    {
                        case NetworkStatus.ErrorNeedRestart:
                        case NetworkStatus.NotStarted:
                        case NetworkStatus.Paused:
                            {
                                if (Status != NetworkStatus.Paused)
                                {
                                    Status = NetworkStatus.Starting;
                                    //MyLogger.Assert(downloader == null && windowsFile == null && fileStream == null);
                                    windowsFile = await CreateTemporaryFile();
                                    fileStream = await windowsFile.OpenStreamForWriteAsync();
                                    downloader = new RestRequests.Downloader(CloudFile.Id, fileStream);
                                }
                                Status = NetworkStatus.Networking;
                                var progressChangedEventHandler = new RestRequests.ProgressChangedEventHandler((bytesProcessed, totalLength) =>
                                  {
                                      BytesDownloaded = bytesProcessed;
                                      TotalFileLength = totalLength;
                                      MyLogger.Assert(this.GetType() == typeof(Downloaders.FileDownloader));
                                      OnProgressChanged(BytesDownloaded,TotalFileLength);
                                  });
                                var messageAppendedEventHandler = new MessageAppendedEventHandler((msg) =>
                                  {
                                      OnMessageAppended("Rest: "+msg);
                                  });
                                downloader.ProgressChanged += progressChangedEventHandler;
                                downloader.MessageAppended += messageAppendedEventHandler;
                                await downloader.DownloadAsync();
                                downloader.ProgressChanged -= progressChangedEventHandler;
                                downloader.MessageAppended -= messageAppendedEventHandler;
                                downloadAgain_index:;
                                switch (downloader.Status)
                                {
                                    case RestRequests.Downloader.DownloadStatus.Completed:
                                        {
                                            fileStream.Dispose();
                                            Status = NetworkStatus.Completed;
                                            return;
                                        }
                                    case RestRequests.Downloader.DownloadStatus.ErrorNeedRestart:
                                        {
                                            OnMessageAppended("Error need restart");
                                            Status = NetworkStatus.ErrorNeedRestart;
                                            return;
                                        }
                                    case RestRequests.Downloader.DownloadStatus.ErrorNeedResume:
                                        {
                                            OnMessageAppended("Error need resume...");
                                            goto downloadAgain_index;
                                        }
                                    case RestRequests.Downloader.DownloadStatus.Paused:
                                        {
                                            Status = NetworkStatus.Paused;
                                            return;
                                        }
                                    case RestRequests.Downloader.DownloadStatus.Downloading:
                                    case RestRequests.Downloader.DownloadStatus.NotStarted:
                                    default: throw new Exception($"downloader.Status: {downloader.Status}");
                                }
                            }
                        case NetworkStatus.Completed:
                        case NetworkStatus.Networking:
                        case NetworkStatus.Starting:
                        default: throw new Exception($"Status: {Status}");
                    }
                }
            }
            //public class Downloader
            //{
            //    public Windows.Storage.StorageFolder WindowsFolder
            //    {
            //        get;
            //        private set;
            //    }
            //    public CloudFile CloudFile
            //    {
            //        get;
            //        private set;
            //    }
            //    public DownloadStatusEnum Status
            //    {
            //        get;
            //        private set;
            //    }
            //    public Downloader(CloudFile _cloudFile, Windows.Storage.StorageFolder _windowsFolder)
            //    {
            //        CloudFile = _cloudFile;
            //        WindowsFolder = _windowsFolder;
            //        Status = DownloadStatusEnum.NotStarted;
            //    }
            //    enum DownloadResult { Completed, Paused, Error, Message };
            //    public List<string> Messages = new List<string>();
            //    public async Task<DownloadResult> StartDownloadAsync()
            //    {
            //        Messages.Clear();
            //        if (CloudFile.IsFolder) await CloudFile.DownloadFolderOnWindowsAsync(WindowsFolder);
            //        else
            //        {

            //        }
            //    }
            //}
        }
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
        #region Events
        private delegate void FileUploadedEventHandler(CloudFile cloudFile, ulong fileSize);
        private delegate void FileUploadProgressChangedEventHandler(string fileName, long bytesSent, long totalLength);
        private delegate void FolderCreatedEventHandler(CloudFile cloudFolder);
        private static event FileUploadedEventHandler FileUploaded;
        private static event FileUploadProgressChangedEventHandler FileUploadProgressChanged;
        private static event FolderCreatedEventHandler FolderCreated;
        #endregion
        #region InnerMethods
        private async Task<Tuple<ulong, ulong, ulong>> CountFilesAndFoldersRecursivelyOnWindowsAsync(Windows.Storage.StorageFolder folder)
        {
            ulong contentLength = 0, fileCount = 0, folderCount = 1;
            var storageFiles = await folder.GetFilesAsync();
            fileCount += (ulong)storageFiles.Count;
            foreach (var storageFile in storageFiles) contentLength += (await storageFile.GetBasicPropertiesAsync()).Size;
            foreach (var storageFolder in await folder.GetFoldersAsync())
            {
                var result = await CountFilesAndFoldersRecursivelyOnWindowsAsync(storageFolder);
                contentLength += result.Item1;
                fileCount += result.Item2;
                folderCount += result.Item3;
            }
            return new Tuple<ulong, ulong, ulong>(contentLength, fileCount, folderCount);
        }
        private async Task<CloudFile> CreateFolderRecursivelyOnWindowsAsync(Windows.Storage.StorageFolder folder)
        {
            MyLogger.Assert(this.IsFolder);
            MyLogger.Log($"Creating folder: {folder.Name} ({this.FullName})");
            var cloudFolder = await this.CreateFolderAsync(folder.Name);
            foreach (var storageFolder in await folder.GetFoldersAsync())
            {
                await cloudFolder.CreateFolderRecursivelyOnWindowsAsync(storageFolder);
            }
            return cloudFolder;
        }
        private async Task UploadFileRecursivelyOnWindowsAsync(Windows.Storage.StorageFolder folder)
        {
            MyLogger.Assert(this.IsFolder);
            MyLogger.Assert(this.Name == folder.Name);
            foreach (var storageFile in await folder.GetFilesAsync())
            {
                MyLogger.Log($"Uploading file: {storageFile.Name} ({this.FullName})");
                await this.UploadFileAsync(storageFile);
            }
            foreach (var storageFolder in await folder.GetFoldersAsync())
            {
                var cloudFolder = await this.GetFolderAsync(storageFolder.Name);
                MyLogger.Assert(cloudFolder != null);
                await cloudFolder.UploadFileRecursivelyOnWindowsAsync(storageFolder);
            }
        }
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
            FileUploaded?.Invoke(ans, 0);
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
            
            throw new NotImplementedException();
        }
        public async Task DownloadFileOnWindowsAsync(Windows.Storage.StorageFile file)
        {
            try
            {
                MyLogger.Assert(!this.IsFolder);
                MyLogger.Assert(this.Name == file.Name);
                var downloader = new CloudFile.Downloaders.FileDownloader(this,file);
                downloader.MessageAppended += new MessageAppendedEventHandler((msg) =>
                    {
                        MyLogger.Log(msg);
                    });
                downloader.StatusChanged += new Networker.NetworkStatusChangedEventHandler(async() =>
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
            if (await this.GetFolderAsync(folder.Name)!=null)
            {
                if (!await MyLogger.Ask($"Folder, \"{folder.Name}\", already existed in \"{this.FullName}\"!\r\nStill want to upload?")) return null;
            }
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
                    cloudFolderToUpload=await CreateFolderRecursivelyOnWindowsAsync(folder);
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
        public async Task<CloudFile> UploadFileAsync(Windows.Storage.StorageFile file)
        {
            try
            {
                var fileSize = (await file.GetBasicPropertiesAsync()).Size;
                if (fileSize == 0)
                {
                    var uploadedFile= await CreateEmptyFileAsync(file.Name);
                    MyLogger.Log($"File upload succeeded!\r\nName: {uploadedFile.Name}\r\nParent: {this.FullName}\r\nID: {uploadedFile.Id}\r\nSize: {fileSize} bytes");
                    MyLogger.Assert(uploadedFile.Name == file.Name);
                    return uploadedFile;
                }
                MyLogger.Assert(this.IsFolder);
                var uploader = new Uploaders.FileUploader(this, file, file.Name);
                indexRetry:;
                await uploader.StartAsync();
                switch(uploader.Status)
                {
                    case Networker.NetworkStatus.Completed:
                        {
                            MyLogger.Log($"File upload succeeded!\r\nName: {file.Name}\r\nParent: {this.FullName}\r\nID: {uploader.UploadedCloudFile.Id}\r\nSize: {fileSize} bytes");
                            var ans = new CloudFile(uploader.UploadedCloudFile.Id, file.Name, false, this);
                            FileUploaded?.Invoke(ans, fileSize);
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
            var request =(await Drive.GetDriveServiceAsync()).Files.Create(
                new Google.Apis.Drive.v3.Data.File
                {
                    Name = folderName,
                    Parents = new List<string> { this.Id },
                    MimeType = Constants.FolderMimeType
                });
            MyLogger.Log($"Creating folder... {folderName} ({this.FullName})");
            var result = await new Func<Task<Google.Apis.Drive.v3.Data.File>, Task<Google.Apis.Drive.v3.Data.File>>((Task<Google.Apis.Drive.v3.Data.File> task) =>
            {
                task.ContinueWith(t =>
                {
                    // NotOnRanToCompletion - this code will be called if the upload fails
                    MyLogger.Log($"Failed to create folder:\r\n{t.Exception}");
                }, TaskContinuationOptions.NotOnRanToCompletion);
                task.ContinueWith(t =>
                {
                    MyLogger.Log($"Folder created successfully: {folderName} ({this.FullName})");
                });
                return task;
            })(request.ExecuteAsync());
            MyLogger.Assert(result.Name == folderName);
            var ans = new CloudFile(result.Id, result.Name, true, this);
            FolderCreated?.Invoke(ans);
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
        public async Task<CloudFile> GetFolderAsync(string folderName,bool assertSingleFolder=true)
        {
            MyLogger.Assert(this.IsFolder);
            var ans = await FilesGetter($"'{this.Id}' in parents and trashed != true and mimeType = '{Constants.FolderMimeType}' and name = '{folderName}'").GetNextPageAsync(2);
            if (ans.Count == 0) return null;
            if(assertSingleFolder) MyLogger.Assert(ans.Count == 1);
            return ans[0];
        }
        #endregion
        #region ClassDefinitions
        public class SearchListGetter
        {
            public bool IsRunning { get; private set; }
            private bool StopRequest;
            string SearchPattern;
            FilesResource.ListRequest ListRequest = null;
            CloudFile Parent;
            public delegate void NewFileListGotEventHandler(List<CloudFile> files);
            public event NewFileListGotEventHandler NewFileListGot;
            public SearchListGetter(CloudFile parent, string pattern)
            {
                Parent = parent;
                SearchPattern = pattern;
                IsRunning = false;
            }
            public async Task ResetAsync()
            {
                if (IsRunning) await StopAsync();
                ListRequest = null;
            }
            public async Task<List<CloudFile>> GetAllPagesAsync()
            {
                var ans = new List<CloudFile>();
                List<CloudFile> tmp;
                while ((tmp = await GetNextPageAsync()) != null) ans.AddRange(tmp);
                return ans;
            }
            public async Task<List<CloudFile>> GetNextPageAsync(int pageSize=100)
            {
                if (ListRequest == null)
                {
                    MyLogger.Assert(!IsRunning);
                    ListRequest = (await Drive.GetDriveServiceAsync()).Files.List();
                    ListRequest.Q = SearchPattern;
                    ListRequest.Fields = "nextPageToken, files(id, name, mimeType)";
                    Log("Getting first page...");
                }
                else Log("Getting next page...");
                if (ListRequest.PageToken == "(END)") return null;
                ListRequest.PageSize = pageSize;
                Google.Apis.Drive.v3.Data.FileList result;
                try
                {
                    result = await ListRequest.ExecuteAsync();
                }
                catch(Exception error)
                {
                    MyLogger.Log(error.ToString());
                    await MyLogger.Alert(error.ToString());
                    await Drive.RefreshAccessTokenAsync();
                    return null;
                }
                //if (result.IncompleteSearch.HasValue && (bool)result.IncompleteSearch) await MyLogger.Alert("This is Incomplete Search");
                var ans = new List<CloudFile>();
                foreach (var file in result.Files)
                {
                    ans.Add(new CloudFile(file.Id, file.Name, file.MimeType == Constants.FolderMimeType, Parent));
                }
                NewFileListGot?.Invoke(ans);
                ListRequest.PageToken = result.NextPageToken;
                if (ListRequest.PageToken == null) ListRequest.PageToken ="(END)";
                return ans;
            }
            public async Task StartAsync()
            {
                MyLogger.Assert(!IsRunning);
                StopRequest = false;
                IsRunning = true;
                while(true)
                {
                    if (StopRequest)
                    {
                        MyLogger.Log("Interrupted.");
                        IsRunning = false;
                        return;
                    }
                    var ans = await GetNextPageAsync();
                    if (ans == null) break;
                    NewFileListGot?.Invoke(ans);
                }
                Log("Done.");
            }
            public async Task StopAsync()
            {
                MyLogger.Assert(IsRunning);
                StopRequest = true;
                while (IsRunning) await Task.Delay(100);
                return;
            }
        }
        #endregion
        public static CloudFile RootFolder { get { return new CloudFile("root", null, true, null); } }
        private static void Log(string log) { MyLogger.Log(log); }
        public CloudFile(string id, string name, bool isFolder, CloudFile _parent) { Id = id; Name = name; IsFolder = isFolder; parent = _parent; }
    }
}
