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
                    return $"[FU]{windowsFolder.Name}  \t↑: {cloudFolder.Name}";
                }
                public static event NewTaskCreatedEventHandler NewFolderUploadCreated;
                CloudFile cloudFolder;
                Windows.Storage.StorageFolder windowsFolder;
                public CloudFile UploadedCloudFolder { get; private set; } = null;
                public FolderUploader(CloudFile _cloudFolder,Windows.Storage.StorageFolder _windowsFolder)
                {
                    cloudFolder = _cloudFolder;
                    windowsFolder = _windowsFolder;
                    NewFolderUploadCreated?.Invoke(this);
                }
                volatile HashSet<Networker> subTasks = new HashSet<Networker>();
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
                                    if (UploadedCloudFolder == null)
                                    {
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
                                    }
                                    //NetworkingCount--;
                                    var windowsFolders = await windowsFolder.GetFilesAsync();
                                    lock (subTasks)
                                    {
                                        foreach (var f in windowsFolders)
                                        {
                                            subTasks.Add(new FileUploader(UploadedCloudFolder, f, f.Name));
                                        }
                                    }
                                    var windowsFiles = await windowsFolder.GetFoldersAsync();
                                    lock (subTasks)
                                    {
                                        foreach (var f in windowsFiles)
                                        {
                                            subTasks.Add(new FolderUploader(UploadedCloudFolder, f));
                                        }
                                    }
                                    OnProgressChanged(0, subTasks.Count);
                                    int progress = 0;
                                    if (Status == NetworkStatus.Paused)
                                    {
                                        IEnumerable<Task> tasks;
                                        lock (subTasks)
                                        {
                                            tasks = subTasks.ToList().Select(async (st) =>
                                             {
                                                 await st.WaitUntilCompletedAsync();
                                                 OnProgressChanged(++progress, subTasks.Count);
                                             });
                                        }
                                        await Task.WhenAll(tasks);
                                    }
                                    else
                                    {
                                        IEnumerable<Task> tasks;
                                        lock (subTasks)
                                        {
                                            tasks = subTasks.ToList().Select(async (st) =>
                                            {
                                                await st.StartUntilCompletedAsync();
                                                OnProgressChanged(++progress, subTasks.Count);
                                            });
                                        }
                                        //foreach (var st in subTasks)
                                        //{
                                        //    await st.StartAsync();
                                        //}
                                        await Task.WhenAll(tasks);
                                    }
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
                                    await Task.WhenAll(subTasks.ToList().Select(async (st) => { await st.StartAsync(); }));
                                    //Status = NetworkStatus.Completed;
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
                        await WaitSemaphoreSlimAsync();
                    }
                }
                protected override async Task PausePrivateAsync()
                {
                    switch(Status)
                    {
                        case NetworkStatus.Networking:
                            {
                                await Task.WhenAll(subTasks.ToList().Select(async (st) => { await st.PauseAsync(); }));
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
