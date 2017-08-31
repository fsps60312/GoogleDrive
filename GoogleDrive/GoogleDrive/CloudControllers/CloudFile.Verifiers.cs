using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

namespace GoogleDrive
{

    partial class CloudFile
    {
        public class Verifiers
        {
            public class FileVerifier : Networker
            {
                public static event NewTaskCreatedEventHandler NewFileVerifierCreated;
                Func<Task> startAction = null;
                protected override async Task StartPrivateAsync()
                {
                    Status = await VerifyFile();
                }
                protected override async Task PausePrivateAsync()
                {
                    await Task.Delay(0);
                    OnMessageAppended("Currently not supported to Pause");
                }
                public override async Task ResetAsync()
                {
                    await Task.Delay(0);
                    OnMessageAppended("Currently not supported to Reset");
                }
                CloudFile cloudFile;
                Windows.Storage.StorageFile windowsFile;
                private async Task<NetworkStatus> VerifyFile()
                {
                    Status = NetworkStatus.Networking;
                    OnProgressChanged(0, 1);
                    if (cloudFile.Name != windowsFile.Name)
                    {
                        OnMessageAppended($"Name not consistent, Cloud: {cloudFile.Name}, Local: {windowsFile.Name}");
                        return NetworkStatus.ErrorNeedRestart;
                    }
                    var stream = await windowsFile.OpenStreamForReadAsync();
                    OnMessageAppended("Getting local MD5 checksum...");
                    var localCheckSum = await Libraries.GetSha256ForWindowsStorageFile(stream);
                    OnMessageAppended("Getting cloud MD5 checksum...");
                    var cloudCheckSum = await Libraries.GetSha256ForCloudFileById(cloudFile.Id);
                    if (localCheckSum != cloudCheckSum)
                    {
                        OnMessageAppended($"Content not consistent, Cloud: {cloudCheckSum}, Local: {localCheckSum}");
                        return NetworkStatus.ErrorNeedRestart;
                    }
                    else
                    {
                        OnMessageAppended($"Verification succeeded, Name: {cloudFile.Name}, Local: {cloudCheckSum}");
                        OnProgressChanged(1, 1);
                        return NetworkStatus.Completed;
                    }
                }
                public FileVerifier(CloudFile _cloudFile, Windows.Storage.StorageFile _windowsFile)
                {
                    this.cloudFile = _cloudFile;
                    this.windowsFile = _windowsFile;
                    MyLogger.Assert(!this.cloudFile.IsFolder);
                    NewFileVerifierCreated?.Invoke(this);
                }
            }
            public class FolderVerifier : Networker
            {
                public static event NewTaskCreatedEventHandler NewFolderVerifierCreated;
                public delegate void TotalProgressChangedEventHandler(long difference);
                public event TotalProgressChangedEventHandler TotalProgressChanged,CurrentProgressChanged;
                protected override async Task StartPrivateAsync()
                {
                    Status = await VerifyFolder();
                }
                protected override async Task PausePrivateAsync()
                {
                    await Task.Delay(0);
                    OnMessageAppended("Currently not supported to Pause");
                }
                public override async Task ResetAsync()
                {
                    await Task.Delay(0);
                    OnMessageAppended("Currently not supported to Reset");
                }
                CloudFile cloudFolder;
                Windows.Storage.StorageFolder windowsFolder;
                private async Task<NetworkStatus> VerifyFolder()
                {
                    Status = NetworkStatus.Networking;
                    long currentProgress=0, totalProgress = 1;
                    OnProgressChanged(currentProgress, totalProgress);
                    if (cloudFolder.Name != windowsFolder.Name)
                    {
                        OnMessageAppended($"Name not consistent, Cloud: {cloudFolder.Name}, Local: {windowsFolder.Name}");
                        return NetworkStatus.ErrorNeedRestart;
                    }
                    var folderVerifiers = new List<Verifiers.FolderVerifier>();
                    {
                        Dictionary<string, CloudFile> cloudSubFolders = new Dictionary<string, CloudFile>();
                        {
                            var cloudFoldersGetter = cloudFolder.FoldersGetter();
                            var list = await cloudFoldersGetter.GetNextPageAsync();
                            while (list != null)
                            {
                                foreach (var f in list)
                                {
                                    if (cloudSubFolders.ContainsKey(f.Name))
                                    {
                                        OnMessageAppended($"Cloud Folder Name duplicated: {f.Name}");
                                        return NetworkStatus.ErrorNeedRestart;
                                    }
                                    cloudSubFolders.Add(f.Name, f);
                                }
                                list = await cloudFoldersGetter.GetNextPageAsync();
                            }
                        }
                        Dictionary<string, Windows.Storage.StorageFolder> localSubFolders = new Dictionary<string, Windows.Storage.StorageFolder>();
                        foreach (var f in await windowsFolder.GetFoldersAsync())
                        {
                            if (!cloudSubFolders.ContainsKey(f.Name))
                            {
                                OnMessageAppended($"Cloud Folder doesn't exist: {f.Name}");
                                return NetworkStatus.ErrorNeedRestart;
                            }
                            localSubFolders.Add(f.Name, f);
                        }
                        foreach (var p in cloudSubFolders)
                        {
                            if (!localSubFolders.ContainsKey(p.Key))
                            {
                                OnMessageAppended($"Local Folder doesn't exist: {p.Key}");
                                return NetworkStatus.ErrorNeedRestart;
                            }
                            folderVerifiers.Add(new FolderVerifier(p.Value, localSubFolders[p.Key]));
                            localSubFolders.Remove(p.Key);
                        }
                        MyLogger.Assert(localSubFolders.Count == 0);
                    }
                    OnProgressChanged(currentProgress, totalProgress += folderVerifiers.Count);
                    TotalProgressChanged?.Invoke(folderVerifiers.Count);
                    var fileVerifiers = new List<Verifiers.FileVerifier>();
                    {
                        Dictionary<string, CloudFile> cloudSubFiles = new Dictionary<string, CloudFile>();
                        {
                            var cloudFilesGetter = cloudFolder.FilesGetter();
                            var list = await cloudFilesGetter.GetNextPageAsync();
                            while (list != null)
                            {
                                foreach (var f in list)
                                {
                                    if (cloudSubFiles.ContainsKey(f.Name))
                                    {
                                        OnMessageAppended($"Cloud File Name duplicated: {f.Name}");
                                        return NetworkStatus.ErrorNeedRestart;
                                    }
                                    cloudSubFiles.Add(f.Name, f);
                                }
                                list = await cloudFilesGetter.GetNextPageAsync();
                            }
                        }
                        Dictionary<string, Windows.Storage.StorageFile> localSubFiles = new Dictionary<string, Windows.Storage.StorageFile>();
                        foreach (var f in await windowsFolder.GetFilesAsync())
                        {
                            if (!cloudSubFiles.ContainsKey(f.Name))
                            {
                                OnMessageAppended($"Cloud File doesn't exist: {f.Name}");
                                return NetworkStatus.ErrorNeedRestart;
                            }
                            localSubFiles.Add(f.Name, f);
                        }
                        foreach (var p in cloudSubFiles)
                        {
                            if (!localSubFiles.ContainsKey(p.Key))
                            {
                                OnMessageAppended($"Local File doesn't exist: {p.Key}");
                                return NetworkStatus.ErrorNeedRestart;
                            }
                            fileVerifiers.Add(new FileVerifier(p.Value, localSubFiles[p.Key]));
                            localSubFiles.Remove(p.Key);
                        }
                        MyLogger.Assert(localSubFiles.Count == 0);
                    }
                    OnProgressChanged(++currentProgress, totalProgress += fileVerifiers.Count);
                    TotalProgressChanged?.Invoke(fileVerifiers.Count);
                    CurrentProgressChanged?.Invoke(1);
                    await Task.WhenAll(fileVerifiers.Select(async (verifier) =>
                    {
                        await verifier.StartUntilCompletedAsync();
                        OnProgressChanged(++currentProgress, totalProgress);
                    }));
                    await Task.WhenAll(folderVerifiers.Select(async (verifier) =>
                    {
                        verifier.TotalProgressChanged += (difference) =>
                        {
                            OnProgressChanged(currentProgress, totalProgress += difference);
                            TotalProgressChanged?.Invoke(difference);
                        };
                        verifier.CurrentProgressChanged += (difference) =>
                          {
                              MyLogger.Assert(difference == 1);
                              OnProgressChanged(currentProgress+=difference, totalProgress);
                              CurrentProgressChanged?.Invoke(difference);
                          };
                        await verifier.StartUntilCompletedAsync();
                    }));
                    return NetworkStatus.Completed;
                }
                public FolderVerifier(CloudFile _cloudFolder, Windows.Storage.StorageFolder _windowsFolder)
                {
                    this.cloudFolder = _cloudFolder;
                    this.windowsFolder = _windowsFolder;
                    MyLogger.Assert(this.cloudFolder.IsFolder);
                    NewFolderVerifierCreated?.Invoke(this);
                }
            }
        }
    }
}