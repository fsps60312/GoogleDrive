﻿using System;
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
                public override string ToString()
                {
                    return $"[U]{cloudFile.Name}  \t↑: {cloudFile.FullName}";
                }
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
                public override string ToString()
                {
                    return $"[U]{cloudFolder.Name}  \t↑: {cloudFolder.FullName}";
                }
                public static event NewTaskCreatedEventHandler NewFolderVerifierCreated;
                public delegate void TotalProgressChangedEventHandler(long difference);
                public event TotalProgressChangedEventHandler TotalProgressChanged, CurrentProgressChanged;
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
                Dictionary<string, CloudFile> cloudSubFolders = null;
                Dictionary<string, string> cloudSubFiles = null;
                //Dictionary<string, Windows.Storage.StorageFolder> localSubFolders = null, localSubFiles = null;
                private async Task<bool> GetCloudSubFolders()
                {
                    cloudSubFolders = new Dictionary<string, CloudFile>();
                    var cloudFoldersGetter = cloudFolder.FoldersGetter();
                    cloudFoldersGetter.MessageAppended += (msg) => { OnMessageAppended($"[cloudFoldersGetter]{msg}"); };
                    var list = await cloudFoldersGetter.GetNextPageAsync();
                    if (cloudFoldersGetter.Status == NetworkStatus.ErrorNeedRestart)
                    {
                        OnMessageAppended($"Failed to get next page");
                        cloudSubFolders = null;
                        return false;
                    }
                    while (list != null)
                    {
                        foreach (var f in list)
                        {
                            if (cloudSubFolders.ContainsKey(f.Name))
                            {
                                OnMessageAppended($"Cloud Folder Name duplicated: {f.Name}");
                                cloudSubFolders = null;
                                return false;
                            }
                            cloudSubFolders.Add(f.Name, f);
                        }
                        list = await cloudFoldersGetter.GetNextPageAsync();
                        if (cloudFoldersGetter.Status == NetworkStatus.ErrorNeedRestart)
                        {
                            OnMessageAppended($"Failed to get next page");
                            cloudSubFolders = null;
                            return false;
                        }
                    }
                    return true;
                }
                class temporaryClassForVerifySubfiles
                {
                    public string nextPageToken;
                    public bool incompleteSearch;
                    public class temporaryClassForVerifySubfilesFilesList
                    {
                        public string name, md5Checksum;
                    }
                    public temporaryClassForVerifySubfilesFilesList[] files;
                }
                private async Task<bool> GetCloudSubFiles()
                {
                    cloudSubFiles = new Dictionary<string, string>();
                    var cloudFilesGetter = cloudFolder.FilesGetter();
                    cloudFilesGetter.MessageAppended += (msg) => { OnMessageAppended($"[cloudFilesGetter]{msg}"); };
                    //MyLogger.Log(str);
                    //MyLogger.Log($"{Newtonsoft.Json.JsonConvert.DeserializeObject<temporaryClassForVerifySubfiles>(str)}");
                    //MyLogger.Log($"{Newtonsoft.Json.JsonConvert.DeserializeObject<temporaryClassForVerifySubfiles>(str).files}");
                    if (cloudFilesGetter.Status == NetworkStatus.ErrorNeedRestart)
                    {
                        OnMessageAppended($"Failed to get next page");
                        cloudSubFiles = null;
                        return false;
                    }
                    var str =await cloudFilesGetter.GetNextPageWithFieldsAsync("nextPageToken,incompleteSearch,files(name,md5Checksum)");
                    while (str != null)
                    {
                        var list = Newtonsoft.Json.JsonConvert.DeserializeObject<temporaryClassForVerifySubfiles>(str).files;
                        foreach (var f in list)
                        {
                            if (cloudSubFiles.ContainsKey(f.name))
                            {
                                OnMessageAppended($"Cloud File Name duplicated: {f.name}");
                                cloudSubFiles = null;
                                return false;
                            }
                            cloudSubFiles.Add(f.name, f.md5Checksum);
                        }
                        str = await cloudFilesGetter.GetNextPageWithFieldsAsync("nextPageToken,incompleteSearch,files(name,md5Checksum)");
                        if (cloudFilesGetter.Status == NetworkStatus.ErrorNeedRestart)
                        {
                            OnMessageAppended($"Failed to get next page");
                            cloudSubFiles = null;
                            return false;
                        }
                    }
                    return true;
                }
                private async Task<NetworkStatus> VerifyFolder()
                {
                    Status = NetworkStatus.Networking;
                    long currentProgress = 0, totalProgress = 1;
                    OnProgressChanged(currentProgress, totalProgress);
                    if (cloudFolder.Name != windowsFolder.Name)
                    {
                        OnMessageAppended($"Name not consistent, Cloud: {cloudFolder.Name}, Local: {windowsFolder.Name}");
                        return NetworkStatus.ErrorNeedRestart;
                    }
                    var folderVerifiers = new List<Tuple<CloudFile, Windows.Storage.StorageFolder>>();
                    {
                        if (!await GetCloudSubFolders()) return NetworkStatus.ErrorNeedRestart;
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
                            folderVerifiers.Add(new Tuple<CloudFile, Windows.Storage.StorageFolder>(p.Value, localSubFolders[p.Key]));
                            localSubFolders.Remove(p.Key);
                        }
                        MyLogger.Assert(localSubFolders.Count == 0);
                    }
                    {
                        if (!await GetCloudSubFiles()) return NetworkStatus.ErrorNeedRestart;
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
                        }
                        OnProgressChanged(++currentProgress, totalProgress += cloudSubFiles.Count);
                        CurrentProgressChanged?.Invoke(1);
                        TotalProgressChanged?.Invoke(cloudSubFiles.Count);
                        OnMessageAppended("Verifying subfiles...");
                        int filesVerified = 0;
                        foreach (var p in cloudSubFiles)
                        {
                            var localFile = localSubFiles[p.Key];
                            var stream = await localFile.OpenStreamForReadAsync();
                            var localMd5 =await Libraries.GetSha256ForWindowsStorageFile(stream);
                            stream.Dispose();
                            if (localMd5!=p.Value||p.Value==null)
                            {
                                OnMessageAppended($"{p.Key} content not consistent, Cloud: {p.Value}, Local: {localMd5}");
                                CurrentProgressChanged?.Invoke(-filesVerified-1);
                                TotalProgressChanged?.Invoke(-cloudSubFiles.Count);
                                return NetworkStatus.ErrorNeedRestart;
                            }
                            OnProgressChanged(++currentProgress, totalProgress);
                            CurrentProgressChanged?.Invoke(1);
                            ++filesVerified;
                            localSubFiles.Remove(p.Key);
                        }
                        OnMessageAppended("Subfiles verified.");
                        MyLogger.Assert(localSubFiles.Count == 0);
                    }
                    OnProgressChanged(currentProgress, totalProgress += folderVerifiers.Count);
                    TotalProgressChanged?.Invoke(folderVerifiers.Count);
                    ReleaseSemaphoreSlim();
                    OnMessageAppended("Waiting for subfolders to be verified...");
                    try
                    {
                        await Task.WhenAll(folderVerifiers.Select(async (tuple) =>
                        {
                            var totalProgressChangedEventHandler = new TotalProgressChangedEventHandler((difference) =>
                              {
                                  OnProgressChanged(currentProgress, totalProgress += difference);
                                  TotalProgressChanged?.Invoke(difference);
                              });
                            var currentProgressChangedEventHandler = new TotalProgressChangedEventHandler((difference) =>
                              {
                                  //MyLogger.Assert(difference == 1);
                                  OnProgressChanged(currentProgress += difference, totalProgress);
                                  CurrentProgressChanged?.Invoke(difference);
                              });
                            var verifier = new Verifiers.FolderVerifier(tuple.Item1, tuple.Item2);
                            verifier.TotalProgressChanged += totalProgressChangedEventHandler;
                            verifier.CurrentProgressChanged += currentProgressChangedEventHandler;
                            await verifier.StartUntilCompletedAsync();
                            verifier.TotalProgressChanged -= totalProgressChangedEventHandler;
                            verifier.CurrentProgressChanged -= currentProgressChangedEventHandler;
                        }));
                        return NetworkStatus.Completed;
                    }
                    finally
                    {
                        await WaitSemaphoreSlimAsync();
                    }
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