using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

namespace GoogleDrive
{
    partial class CloudFile
    {
        public partial class Uploaders
        {
            public class FileUploader:Networker
            {
                public override string ToString()
                {
                    return $"[U]{fileName}";
                }
                public delegate void NewFileUploadCreatedEventHandler(FileUploader uploader);
                public static event NewFileUploadCreatedEventHandler NewFileUploadCreated;
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
                    NewFileUploadCreated?.Invoke(this);
                }
                Windows.Storage.StorageFile windowsFile = null;
                RestRequests.Uploader uploader = null;
                System.IO.Stream fileStream = null;
                public override async Task ResetAsync()
                {
                    try
                    {
                        Status = NetworkStatus.NotStarted;
                        if (fileStream != null)
                        {
                            fileStream.Dispose();
                            fileStream = null;
                        }
                        fileStream = await windowsFile.OpenStreamForReadAsync();
                        uploader = new RestRequests.Uploader(new List<string> { CloudFolder.Id }, fileStream, fileName);
                    }
                    catch(Exception error)
                    {
                        OnMessageAppended($"Unexpected: {error}");
                        Status = NetworkStatus.ErrorNeedRestart;
                    }
                }
                public override async Task PauseAsync()
                {
                    await uploader.PauseAsync();
                }
                protected override async Task StartPrivateAsync()
                {
                    try
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
                                        if (fileStream != null)
                                        {
                                            fileStream.Dispose();
                                            fileStream = null;
                                        }
                                        fileStream = await windowsFile.OpenStreamForReadAsync();
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
                                {
                                    OnMessageAppended($"Status: { Status}, no action take to start");
                                    return;
                                }
                            default: throw new Exception($"Status: {Status}");
                        }
                    }
                    catch(Exception error)
                    {
                        OnMessageAppended($"Unexpected: {error}");
                        Status = NetworkStatus.ErrorNeedRestart;
                    }
                }
            }
        }
    }
}
