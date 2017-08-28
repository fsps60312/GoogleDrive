using System;
using System.Threading.Tasks;

namespace GoogleDrive
{

    partial class CloudFile
    {
        public class Modifiers
        {
            public class FolderCreater:Networker
            {
                public delegate void NewFolderCreateCreatedEventHandler(FolderCreater folderCreater);
                public static event NewFolderCreateCreatedEventHandler NewFolderCreateCreated;
                CloudFile cloudFolder;
                string folderName;
                public CloudFile CreatedCloudFolder { get; private set; }
                public FolderCreater(CloudFile _cloudFolder,string _folderName)
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
                        OnMessageAppended("Creating folder...");
                        CreatedCloudFolder = await cloudFolder.CreateFolderAsync(folderName);
                        OnMessageAppended("Folder created");
                    }
                    catch(Exception error)
                    {
                        OnMessageAppended(error.ToString());
                        Status = NetworkStatus.ErrorNeedRestart;
                        return;
                    }
                    Status = NetworkStatus.Completed;
                }
                public override async Task PauseAsync()
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
