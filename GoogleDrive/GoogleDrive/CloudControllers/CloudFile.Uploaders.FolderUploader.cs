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
                                    var fc = new Modifiers.FolderCreater(cloudFolder, windowsFolder.Name);
                                    fc.MessageAppended += (msg) => { OnMessageAppended($"[FC]{msg}"); };
                                    fc.StatusChanged += async delegate
                                    {
                                        if (fc.Status == NetworkStatus.Completed)
                                        {
                                            OnMessageAppended("Folder created");
                                            UploadedCloudFolder = fc.CreatedCloudFolder;
                                            OnProgressChanged(1, 1);
                                        //NetworkingCount--;
                                        foreach (var f in await windowsFolder.GetFilesAsync())
                                            {
                                                subTasks.Add(new FileUploader(fc.CreatedCloudFolder, f, f.Name));
                                            }
                                            foreach (var f in await windowsFolder.GetFoldersAsync())
                                            {
                                                subTasks.Add(new FolderUploader(fc.CreatedCloudFolder, f));
                                            }
                                        //foreach (var st in subTasks)
                                        //{
                                        //    await st.StartAsync();
                                        //}
                                        await Task.WhenAll(subTasks.Select(async (st) => { await st.StartAsync(); }));
                                        //NetworkingCount++;
                                        Status = NetworkStatus.Completed;
                                        }
                                    };
                                    OnMessageAppended("Creating folder...");
                                    await fc.StartAsync();
                                }
                                break;
                            case NetworkStatus.Paused:
                                {
                                    Status = NetworkStatus.Networking;
                                    await Task.WhenAll(subTasks.Select(async (st) => { await st.StartAsync(); }));
                                    Status = NetworkStatus.Completed;
                                }
                                break;
                            default: throw new Exception($"Status: {Status}");
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
                public override async Task PauseAsync()
                {
                    switch(Status)
                    {
                        case NetworkStatus.Networking:
                            {
                                await Task.WhenAll(subTasks.Select(async (st) => { await st.PauseAsync(); }));
                                Status = NetworkStatus.Paused;
                            }break;
                        default: throw new Exception($"Status: {Status}");
                    }
                }
                public override async Task ResetAsync()
                {
                    await Task.Delay(1000);
                    throw new NotImplementedException();
                }
            }
        }
    }
}
