using System;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace GoogleDrive
{
    partial class RestRequests
    {
        public delegate void ChunkSentEventHandler(long chunkSize);
        const int MinChunkSize = 262144/* * 2*/;
        private static async Task<string> LogHttpWebResponse(HttpWebResponse response, bool readStream)
        {
            string ans = $"Http response: {response.StatusCode} ({(int)response.StatusCode})\r\n";
            StringBuilder sb = new StringBuilder();
            foreach (var key in response.Headers.AllKeys) sb.AppendLine($"{key}:{JsonConvert.SerializeObject(response.Headers[key])}");
            ans += sb.ToString() + "\r\n";
            if (readStream)
            {
                var reader = new System.IO.StreamReader(response.GetResponseStream());
                ans += await reader.ReadToEndAsync() + "\r\n";
                reader.Dispose();
            }
            return ans;
        }
        static volatile int cnt = 0;
        static DateTime time = DateTime.Now;
        static System.Threading.SemaphoreSlim semaphoreSlimGetHttpResponse = new System.Threading.SemaphoreSlim(50, 50);
        private static async Task<HttpWebResponse> GetHttpResponseAsync(HttpWebRequest request)
        {
            await semaphoreSlimGetHttpResponse.WaitAsync();
            try
            {
                cnt++;
                if ((DateTime.Now - time).TotalSeconds > 30)
                {
                    MyLogger.Log($"{cnt} requests from {DateTime.Now} to {time}");
                    time = DateTime.Now;
                    cnt = 0;
                    //await MyLogger.Alert($"cnt={a}");
                }
                HttpWebResponse ans;
                int minisecs = 500;
                indexRetry:;
                try
                {
                    ans = (await request.GetResponseAsync()) as HttpWebResponse;
                }
                catch (WebException error)
                {
                    ans = error.Response as HttpWebResponse;
                }
                finally
                {
                    request.Abort();
                }
                if (ans == null)
                {
                    MyLogger.Log("Got null response");
                    return null;
                }
                switch (ans.StatusCode)
                {
                    case HttpStatusCode.InternalServerError:
                    case HttpStatusCode.BadGateway:
                    case HttpStatusCode.ServiceUnavailable:
                    case HttpStatusCode.GatewayTimeout:
                    case HttpStatusCode.Forbidden:
                        {
                            MyLogger.Log(await LogHttpWebResponse(ans, true));
                            if (minisecs > Constants.MaxTimeToWait)
                            {
                                MyLogger.Log("Attempted to reconnect but still failed.");
                                return ans;
                            }
                            else
                            {
                                MyLogger.Log($"Response: {ans.StatusCode} ({(int)ans.StatusCode}), waiting for {minisecs} ms and try again...");
                                await Task.Delay(minisecs);
                                minisecs *= 2;
                                goto indexRetry;
                            }
                        }
                    case HttpStatusCode.Unauthorized:
                        {
                            MyLogger.Log("Http response: Unauthorized (401). May due to expired access token, refreshing...");
                            MyLogger.Log(await LogHttpWebResponse(ans, true));
                            MyLogger.Assert(Array.IndexOf(request.Headers.AllKeys, "Authorization") != -1);
                            request.Headers["Authorization"] = "Bearer " + (await Drive.RefreshAccessTokenAsync());
                            await Task.Delay(500);
                            goto indexRetry;
                        }
                    default: return ans;
                }
            }
            finally
            {
                lock (semaphoreSlimGetHttpResponse) semaphoreSlimGetHttpResponse.Release();
            }
        }
        public delegate void ProgressChangedEventHandler(long bytesProcessed, long totalLength);
        public class SearchListGetter
        {
            public event MessageAppendedEventHandler MessageAppended;
            public delegate void NewFileListGotEventHandler(List<Tuple<string, string, string>> fileList);
            public event NewFileListGotEventHandler NewFileListGot;
            public enum SearchStatus {NotStarted,Searching,Paused,Completed,ErrorNeedResume,ErrorNeedRestart }
            public SearchStatus Status = SearchStatus.NotStarted;
            string searchPattern, pageToken = null;
            int pageSize = 100;
            public int PageSize
            {
                get { return pageSize; }
                set { pageSize = value; }
            }
            public List<Tuple<string, string, string>> FileListGot // id, name, mimeType
            {
                get;
                private set;
            } = new List<Tuple<string, string, string>>();
            public SearchListGetter(string _searchPattern)
            {
                searchPattern = _searchPattern;
            }
            class temporaryClassForResponseBody
            {
                public string nextPageToken;
                public bool incompleteSearch;
                public class temporaryClassForResponseBodyFilesList
                {
                    public string id, name, mimeType;
                }
                public temporaryClassForResponseBodyFilesList[] files;
            }
            public async Task GetNextPageAsync()
            {
                string url = "https://www.googleapis.com/drive/v3/files?corpora=user";
                url += $"&pageSize={pageSize}";
                if (pageToken != null) url+=$"&pageToken={pageToken}";
                url += $"&q={System.Net.WebUtility.UrlEncode(searchPattern)}";
                HttpWebRequest request = WebRequest.CreateHttp(url);
                //MessageAppended?.Invoke($"url: {url}");
                request.Headers["Authorization"] = "Bearer " + (await Drive.GetAccessTokenAsync());
                request.Method = "GET";
                using (var response = await GetHttpResponseAsync(request))
                {
                    if (response == null)
                    {
                        MessageAppended?.Invoke("Null response");
                        Status = SearchStatus.ErrorNeedResume;
                        return;
                    }
                    MessageAppended?.Invoke($"Http response: {response.StatusCode} ({(int)response.StatusCode})");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string resultText;
                        using (var responseStream = response.GetResponseStream())
                        {
                            using (var reader = new System.IO.StreamReader(responseStream))
                            {
                                resultText = await reader.ReadToEndAsync();
                            }
                        }
                        var result = JsonConvert.DeserializeObject<temporaryClassForResponseBody>(resultText);
                        if (result.incompleteSearch)
                        {
                            MessageAppended?.Invoke("Warning! This is an Incomplete Search");
                            MyLogger.Log("Warning! This is an Incomplete Search");
                        }
                        FileListGot.Clear();
                        foreach (var file in result.files)
                        {
                            FileListGot.Add(new Tuple<string, string, string>(file.id, file.name, file.mimeType));
                        }
                        NewFileListGot?.Invoke(FileListGot);
                        if (result.nextPageToken == null)
                        {
                            Status = SearchStatus.Completed;
                        }
                        else
                        {
                            pageToken = result.nextPageToken;
                            Status = SearchStatus.Paused;
                        }
                        return;
                    }
                    else
                    {
                        MessageAppended?.Invoke("Http response isn't OK!");
                        MessageAppended?.Invoke(await LogHttpWebResponse(response, true));
                        Status = SearchStatus.ErrorNeedResume;
                        return;
                    }
                }
            }
        }
    }
}
