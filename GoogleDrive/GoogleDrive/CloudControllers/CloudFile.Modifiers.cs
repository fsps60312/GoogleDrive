using System;
using System.Threading.Tasks;

namespace GoogleDrive
{

    partial class CloudFile
    {
        public class Modifiers
        {
            public class FolderCreator:Networker
            {
                public override string ToString()
                {
                    return $"[FC]{folderName}  \t↑: {cloudFolder.Name}";
                }
                public static event NewTaskCreatedEventHandler NewFolderCreateCreated;
                CloudFile cloudFolder;
                string folderName;
                public CloudFile CreatedCloudFolder { get; private set; }
                public FolderCreator(CloudFile _cloudFolder,string _folderName)
                {
                    cloudFolder = _cloudFolder;
                    folderName = _folderName;
                    NewFolderCreateCreated?.Invoke(this);
                }
                protected override async Task StartPrivateAsync()
                {
                    Status = NetworkStatus.Networking;
                    try
                    {
                        OnTotalFoldersRemainChanged(1);
                        OnMessageAppended("Creating folder...");
                        var folderCreator = new RestRequests.FileCreator(cloudFolder.Id, folderName, true);
                        var eventHandler = new MessageAppendedEventHandler((log) => { OnMessageAppended($"[Rest]{log}"); });
                        folderCreator.MessageAppended += eventHandler;
                        await folderCreator.Start();
                        folderCreator.MessageAppended -= eventHandler;
                        switch (folderCreator.Status)
                        {
                            case RestRequests.FileCreator.FileCreatorStatus.Completed:
                                {
                                    CreatedCloudFolder = new CloudFile(folderCreator.Result, folderName, true, cloudFolder);
                                    //CreatedCloudFolder = await cloudFolder.CreateFolderAsync(folderName);
                                    OnMessageAppended("Folder created");
                                    Status = NetworkStatus.Completed;
                                    return;
                                }
                            case RestRequests.FileCreator.FileCreatorStatus.ErrorNeedRestart:
                                {
                                    Status = NetworkStatus.ErrorNeedRestart;
                                    return;
                                }
                            default:
                                {
                                    throw new Exception($"Status: {Status}");
                                }
                        }
                    }
                    catch(Exception error)
                    {
                        OnMessageAppended(error.ToString());
                        Status = NetworkStatus.ErrorNeedRestart;
                        return;
                    }
                    finally
                    {
                        OnTotalFoldersRemainChanged(-1);
                    }
                }
                protected override async Task PausePrivateAsync()
                {
                    while (Status != NetworkStatus.Completed) await Task.Delay(100);
                }
                public override async Task ResetAsync()
                {
                    await PauseAsync();
                }
            }
        }
    }
}
