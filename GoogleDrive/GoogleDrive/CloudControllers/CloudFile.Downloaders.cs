using System;
using System.Threading.Tasks;
using System.IO;

namespace GoogleDrive
{
    partial class CloudFile
    {
        public class Downloaders
        {
            public class FileDownloader:Networker
            {
                public override string ToString()
                {
                    return $"[D]{CloudFile.Name}";
                }
                public delegate void NewFileDownloadCreatedEventHandler(FileDownloader downloader);
                public static event NewFileDownloadCreatedEventHandler NewFileDownloadCreated;
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
                    NewFileDownloadCreated?.Invoke(this);
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
                protected override async Task PausePrivateAsync()
                {
                    await downloader.PauseAsync();
                }
                protected override async Task StartPrivateAsync()
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
    }
}
