using System;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace GoogleDrive
{
    partial class RestRequests
    {
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
            public string NextPageString
            {
                get;
                private set;
            }
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
            public async Task GetNextPageAsync(string fields= "nextPageToken,incompleteSearch,files(id,name,mimeType)")
            {
                string url = $"https://www.googleapis.com/drive/v3/files?corpora=user&fields={fields}";
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
                        NextPageString = resultText;
                        var result = JsonConvert.DeserializeObject<temporaryClassForResponseBody>(resultText);
                        if (result.incompleteSearch)
                        {
                            MessageAppended?.Invoke("Warning! This is an Incomplete Search");
                            MyLogger.Log("Warning! This is an Incomplete Search");
                        }
                        FileListGot.Clear();
                        if (result.files != null)
                        {
                            foreach (var file in result.files)
                            {
                                FileListGot.Add(new Tuple<string, string, string>(file.id, file.name, file.mimeType));
                            }
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
