using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Google.Apis.Drive.v3;

namespace GoogleDrive
{
    partial class CloudFile
    {
        public class SearchUnderSpecificFolderListGetter
        {
            public event MessageAppendedEventHandler MessageAppended;
            string searchPattern;
            CloudFile parent;
            public delegate void NewFileListGotEventHandler(List<CloudFile> files);
            public event NewFileListGotEventHandler NewFileListGot;
            public Networker.NetworkStatus Status = Networker.NetworkStatus.NotStarted;
            RestRequests.SearchListGetter searchListGetter = null;
            bool pauseRequest = false;
            public SearchUnderSpecificFolderListGetter(CloudFile _parent, string pattern)
            {
                parent = _parent;
                searchPattern = $"'{parent.Id}' in parents and ({pattern})";
            }
            public async Task ResetAsync()
            {
                await PauseAsync();
                searchListGetter = null;
            }
            public async Task<List<CloudFile>> GetAllPagesAsync()
            {
                var ans = new List<CloudFile>();
                List<CloudFile> tmp;
                while ((tmp = await GetNextPageAsync()) != null) ans.AddRange(tmp);
                return ans;
            }
            public async Task<List<CloudFile>> GetNextPageAsync(int pageSize = 100)
            {
                Status = Networker.NetworkStatus.Networking;
                if (searchListGetter == null)
                {
                    searchListGetter = new RestRequests.SearchListGetter(searchPattern);
                    searchListGetter.MessageAppended += (msg) => { MessageAppended?.Invoke($"[Rest]{msg}"); };
                    MessageAppended?.Invoke("Getting first page...");
                }
                else MessageAppended?.Invoke("Getting next page...");
                if (searchListGetter.Status == RestRequests.SearchListGetter.SearchStatus.Completed)
                {
                    Status = Networker.NetworkStatus.Completed;
                    return null;
                }
                searchListGetter.PageSize = pageSize;
                int timeToWait = 500;
                index_tryAgain:;
                await searchListGetter.GetNextPageAsync();
                if (searchListGetter.Status == RestRequests.SearchListGetter.SearchStatus.Paused)
                {
                    var ans = searchListGetter.FileListGot.Select((file) =>
                    {
                        return new CloudFile(file.Item1, file.Item2, file.Item3 == Constants.FolderMimeType, parent);
                    }).ToList();
                    Status = Networker.NetworkStatus.Paused;
                    return ans;
                }
                else if (searchListGetter.Status == RestRequests.SearchListGetter.SearchStatus.Completed)
                {
                    var ans = searchListGetter.FileListGot.Select((file) =>
                    {
                        return new CloudFile(file.Item1, file.Item2, file.Item3 == Constants.FolderMimeType, parent);
                    }).ToList();
                    Status = Networker.NetworkStatus.Completed;
                    return ans;
                }
                else
                {
                    MessageAppended?.Invoke($"searchListGetter.Status: {searchListGetter.Status}");
                    if (searchListGetter.Status == RestRequests.SearchListGetter.SearchStatus.ErrorNeedResume)
                    {
                        if (timeToWait > Constants.MaxTimeToWait)
                        {
                            Status = Networker.NetworkStatus.ErrorNeedRestart;
                            return null;
                        }
                        MessageAppended?.Invoke($"Waiting for {timeToWait} milisecs and try again...");
                        await Task.Delay(timeToWait);
                        timeToWait *= 2;
                        goto index_tryAgain;
                    }
                    Status = Networker.NetworkStatus.ErrorNeedRestart;
                    return null;
                }
            }
            public async Task StartAsync()
            {
                pauseRequest = false;
                while (true)
                {
                    if (pauseRequest)
                    {
                        MessageAppended?.Invoke("Paused.");
                        Status = Networker.NetworkStatus.Paused;
                        return;
                    }
                    var ans = await GetNextPageAsync();
                    if (ans == null) break;
                    NewFileListGot?.Invoke(ans);
                }
                MessageAppended?.Invoke("Done.");
            }
            public async Task PauseAsync()
            {
                pauseRequest = true;
                while (Status == Networker.NetworkStatus.Networking)
                {
                    await Task.Delay(100);
                }
                return;
            }
        }
        //public class SearchListGetter
        //{
        //    private bool IsRunning { get; set; }
        //    private bool StopRequest;
        //    string SearchPattern;
        //    FilesResource.ListRequest ListRequest = null;
        //    CloudFile Parent;
        //    public delegate void NewFileListGotEventHandler(List<CloudFile> files);
        //    public event NewFileListGotEventHandler NewFileListGot;
        //    public SearchListGetter(CloudFile parent, string pattern)
        //    {
        //        Parent = parent;
        //        SearchPattern = pattern;
        //        IsRunning = false;
        //    }
        //    public async Task ResetAsync()
        //    {
        //        if (IsRunning) await StopAsync();
        //        ListRequest = null;
        //    }
        //    public async Task<List<CloudFile>> GetAllPagesAsync()
        //    {
        //        var ans = new List<CloudFile>();
        //        List<CloudFile> tmp;
        //        while ((tmp = await GetNextPageAsync()) != null) ans.AddRange(tmp);
        //        return ans;
        //    }
        //    public async Task<List<CloudFile>> GetNextPageAsync(int pageSize=100)
        //    {
        //        if (ListRequest == null)
        //        {
        //            MyLogger.Assert(!IsRunning);
        //            ListRequest = (await Drive.GetDriveServiceAsync()).Files.List();
        //            ListRequest.Q = SearchPattern;
        //            ListRequest.Fields = "nextPageToken, files(id, name, mimeType)";
        //            Log("Getting first page...");
        //        }
        //        else Log("Getting next page...");
        //        if (ListRequest.PageToken == "(END)") return null;
        //        ListRequest.PageSize = pageSize;
        //        Google.Apis.Drive.v3.Data.FileList result;
        //        try
        //        {
        //            result = await ListRequest.ExecuteAsync();
        //        }
        //        catch(Exception error)
        //        {
        //            MyLogger.Log(error.ToString());
        //            await MyLogger.Alert(error.ToString());
        //            await Drive.RefreshAccessTokenAsync();
        //            return null;
        //        }
        //        //if (result.IncompleteSearch.HasValue && (bool)result.IncompleteSearch) await MyLogger.Alert("This is Incomplete Search");
        //        var ans = new List<CloudFile>();
        //        foreach (var file in result.Files)
        //        {
        //            ans.Add(new CloudFile(file.Id, file.Name, file.MimeType == Constants.FolderMimeType, Parent));
        //        }
        //        NewFileListGot?.Invoke(ans);
        //        ListRequest.PageToken = result.NextPageToken;
        //        if (ListRequest.PageToken == null) ListRequest.PageToken ="(END)";
        //        return ans;
        //    }
        //    public async Task StartAsync()
        //    {
        //        MyLogger.Assert(!IsRunning);
        //        StopRequest = false;
        //        IsRunning = true;
        //        while(true)
        //        {
        //            if (StopRequest)
        //            {
        //                MyLogger.Log("Interrupted.");
        //                IsRunning = false;
        //                return;
        //            }
        //            var ans = await GetNextPageAsync();
        //            if (ans == null) break;
        //            NewFileListGot?.Invoke(ans);
        //        }
        //        Log("Done.");
        //    }
        //    public async Task StopAsync()
        //    {
        //        MyLogger.Assert(IsRunning);
        //        StopRequest = true;
        //        while (IsRunning) await Task.Delay(100);
        //        return;
        //    }
        //}
    }
}
