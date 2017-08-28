using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;

namespace GoogleDrive
{
    partial class CloudFile
    {
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
    }
}
