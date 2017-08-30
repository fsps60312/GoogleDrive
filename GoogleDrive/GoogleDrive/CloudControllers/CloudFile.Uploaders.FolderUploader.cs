using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace GoogleDrive
{
    partial class CloudFile
    {
        public partial class Uploaders
        {
            public class FolderUploader:Networker
            {
                public override string ToString()
                {
                    return $"[FU]{windowsFolder.Name}";
                }
                public delegate void NewFolderUploadCreatedEventHandler(FolderUploader uploader);
                public static event NewFolderUploadCreatedEventHandler NewFolderUploadCreated;
                CloudFile cloudFolder;
                Windows.Storage.StorageFolder windowsFolder;
                public CloudFile UploadedCloudFolder { get; private set; } = null;
                public FolderUploader(CloudFile _cloudFolder,Windows.Storage.StorageFolder _windowsFolder)
                {
                    cloudFolder = _cloudFolder;
                    windowsFolder = _windowsFolder;
                    NewFolderUploadCreated?.Invoke(this);
                }
                HashSet<Networker> subTasks = new HashSet<Networker>();
                protected override async Task StartPrivateAsync()
                {
                    ReleaseSemaphoreSlim();
                    try
                    {
                        switch (Status)
                        {
                            case NetworkStatus.NotStarted:
                                {
                                    Status = NetworkStatus.Networking;
                                    var fc = new Modifiers.FolderCreator(cloudFolder, windowsFolder.Name);
                                    var messageAppendedEventHandler = new MessageAppendedEventHandler((msg) => { OnMessageAppended($"[FC]{msg}"); });
                                    OnMessageAppended("Creating folder...");
                                    fc.MessageAppended += messageAppendedEventHandler;
                                    await fc.StartUntilCompletedAsync();
                                    fc.MessageAppended -= messageAppendedEventHandler;
                                    //switch (fc.Status)
                                    //{
                                    //    case NetworkStatus.Completed:
                                    //        {
                                    OnMessageAppended("Folder created");
                                    UploadedCloudFolder = fc.CreatedCloudFolder;
                                    OnProgressChanged(0, 0);
                                    //NetworkingCount--;
                                    foreach (var f in await windowsFolder.GetFilesAsync())
                                    {
                                        subTasks.Add(new FileUploader(fc.CreatedCloudFolder, f, f.Name));
                                    }
                                    foreach (var f in await windowsFolder.GetFoldersAsync())
                                    {
                                        subTasks.Add(new FolderUploader(fc.CreatedCloudFolder, f));
                                    }
                                    OnProgressChanged(0, subTasks.Count);
                                    //foreach (var st in subTasks)
                                    //{
                                    //    await st.StartAsync();
                                    //}
                                    int progress = 0;
                                    await Task.WhenAll(subTasks.Select(async (st) =>
                                    {
                                        await st.StartUntilCompletedAsync();
                                        OnProgressChanged(++progress, subTasks.Count);
                                    }));
                                    //NetworkingCount++;
                                    Status = NetworkStatus.Completed;
                                    return;
                                    //        }
                                    //    case NetworkStatus.ErrorNeedRestart:
                                    //        {
                                    //            this.Status = NetworkStatus.ErrorNeedRestart;
                                    //            return;
                                    //        }
                                    //    default: throw new Exception($"Status: {Status}");
                                    //}
                                }
                            case NetworkStatus.Paused:
                                {
                                    Status = NetworkStatus.Networking;
                                    await Task.WhenAll(subTasks.Select(async (st) => { await st.StartAsync(); }));
                                    Status = NetworkStatus.Completed;
                                }break;
                            default:
                                {
                                    OnMessageAppended($"Status: {Status}, no way to start");
                                }break;
                        }
                    }
                    catch(Exception error)
                    {
                        throw error;
                    }
                    finally
                    {
                        await WaitSemaphoreSlim();
                    }
                }
                protected override async Task PausePrivateAsync()
                {
                    switch(Status)
                    {
                        case NetworkStatus.Networking:
                            {
                                await Task.WhenAll(subTasks.Select(async (st) => { await st.PauseAsync(); }));
                                Status = NetworkStatus.Paused;
                            }break;
                        default:
                            {
                                OnMessageAppended($"Status: {Status}, no way to pause");
                            }break;
                    }
                }
                public override async Task ResetAsync()
                {
                    await MyLogger.Alert("FolderUploader currently not support Reset()");
                    //await Task.Delay(1000);
                    //throw new NotImplementedException();
                }
            }
        }
    }
}
