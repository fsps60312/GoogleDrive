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
        const int MinChunkSize = 262144 * 2;
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
        private static async Task<HttpWebResponse> GetHttpResponseAsync(HttpWebRequest request)
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
        public delegate void ProgressChangedEventHandler(long bytesProcessed, long totalLength);
        public class FileCreator
        {
            public enum FileCreatorStatus {NotStarted, Creating,Completed, ErrorNeedRestart };
            public FileCreatorStatus Status { get; private set; } = FileCreatorStatus.NotStarted;
            public event MessageAppendedEventHandler MessageAppended;
            string cloudId, name;
            bool isFolder;
            public string Result { get; private set; }
            public FileCreator(string _cloudId,string _name,bool _isFolder)
            {
                cloudId = _cloudId;
                name = _name;
                isFolder = _isFolder;
                //MessageAppended += (log) => { MyLogger.Log(log); };
            }
            public async Task Start()
            {
                int timeToWait = 500;
                while(true)
                {
                    await StartPrivate();
                    switch (Status)
                    {
                        case FileCreatorStatus.ErrorNeedRestart:
                            {
                                if (timeToWait >Constants.MaxTimeToWait) return;
                                MessageAppended?.Invoke($"Waiting for {timeToWait} and try again...");
                                timeToWait *= 2;
                            }break;
                        case FileCreatorStatus.Completed:
                            {
                                return;
                            }
                        default:
                            {
                                throw new Exception($"Status: {Status}");
                            }
                    }
                }
            }
            private async Task StartPrivate()
            {
                Status = FileCreatorStatus.Creating;
                MessageAppended?.Invoke("Creating Folder...");
                string json = $"{{\"name\":\"{name}\",\"parents\":[\"{cloudId}\"]";
                if(isFolder)
                {
                    json += ",\"mimeType\": \"application/vnd.google-apps.folder\"";
                }
                json += "}";
                MessageAppended?.Invoke(json);
                HttpWebRequest request = WebRequest.CreateHttp("https://www.googleapis.com/drive/v3/files");
                var bytes = Encoding.UTF8.GetBytes(json);
                request.Headers["Content-Type"] = "application /json; charset=UTF-8";
                request.Headers["Content-Length"] = bytes.Length.ToString();
                request.Headers["Authorization"] = "Bearer " + (await Drive.GetAccessTokenAsync());
                request.Method = "POST";
                using (System.IO.Stream requestStream = await request.GetRequestStreamAsync())
                {
                    await requestStream.WriteAsync(bytes, 0, bytes.Length);
                }
                using (var response = await GetHttpResponseAsync(request))
                {
                    if (response == null)
                    {
                        MessageAppended?.Invoke("Null response");
                        Status = FileCreatorStatus.ErrorNeedRestart;
                        return;
                    }
                    MessageAppended?.Invoke($"Http response: {response.StatusCode} ({(int)response.StatusCode})");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                        {
                            var jsonObj = JsonConvert.DeserializeObject<Dictionary<string, string>>(await reader.ReadToEndAsync());
                            if (!jsonObj.ContainsKey("id"))
                            {
                                MessageAppended?.Invoke("response.Headers doesn't contain a key for \"id\"!");
                                MessageAppended?.Invoke(await LogHttpWebResponse(response, true));
                                Status = FileCreatorStatus.ErrorNeedRestart;
                                return;
                            }
                            Result= jsonObj["id"];
                            MessageAppended?.Invoke($"Folder {name} ({Result}) created!");
                            Status = FileCreatorStatus.Completed;
                            return;
                        }
                    }
                    else
                    {
                        MessageAppended?.Invoke("Http response isn't OK!");
                        MessageAppended?.Invoke(await LogHttpWebResponse(response, true));
                        Status = FileCreatorStatus.ErrorNeedRestart;
                        return;
                    }
                }
            }
        }
    }
}
