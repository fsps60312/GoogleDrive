using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;

namespace GoogleDrive
{
    class RestRequests
    {
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
        private static async Task<HttpWebResponse> GetHttpResponseAsync(HttpWebRequest request)
        {
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
                    {
                        if (minisecs > 500 * 16)
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
                        goto indexRetry;
                    }
                default: return ans;
            }
        }
        public delegate void ProgressChangedEventHandler(long bytesProcessed, long totalLength);
        //class CloudFileJsonObject
        //{
        //    public string kind, id, name, mimeType;
        //}
        public class FileGetter
        {
            public FileGetter(string _fileId)
            {
                fileId = _fileId;
            }
            string fileId;
            public async Task<string>GetFileAsync()
            {
                var request = WebRequest.CreateHttp($"https://www.googleapis.com/drive/v2/files/{fileId}");
                request.Headers["Authorization"] = "Bearer " + (await Drive.GetAccessTokenAsync());
                request.Method = "GET";
                using (var response = await GetHttpResponseAsync(request))
                {
                    if(response==null)
                    {
                        MyLogger.Log("Null response");
                        return null;
                    }
                    MyLogger.Log($"Http response: {response.StatusCode} ({(int)response.StatusCode})");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return await LogHttpWebResponse(response, true);
                    }
                    else
                    {
                        MyLogger.Log("Http response isn't OK!");
                        MyLogger.Log(await LogHttpWebResponse(response, true));
                        return null;
                    }
                }
            }
        }
        public class Uploader
        {
            //resumable upload REST API: https://developers.google.com/drive/v3/web/resumable-upload
            #region Private Fields & Methods
            System.IO.Stream fileStream;
            string resumableUri = null, fileName;
            IList<string> parents;
            long byteReceivedSoFar;
            public enum UploadStatus { NotStarted,Creating,Created, Uploading, Completed, Paused, ErrorNeedRestart, ErrorNeedResume };
            public UploadStatus Status
            {
                get;
                private set;
            } = UploadStatus.NotStarted;
            public string CloudFileId
            {
                get;
                private set;
            } = null;
            public event MessageAppendedEventHandler MessageAppended;
            bool pauseRequest = false;
            private async Task VerifyCheckSum()
            {
                MessageAppended?.Invoke("Calculating checksum...");
                string cloudCheckSum = await Libraries.GetSha256ForCloudFileById(CloudFileId);
                MessageAppended?.Invoke($"Cloud: {cloudCheckSum}");
                fileStream.Position = 0;
                string localCheckSum = await Libraries.GetSha256ForWindowsStorageFile(fileStream);
                MessageAppended?.Invoke($"Local: {localCheckSum}");
                if (cloudCheckSum == localCheckSum)
                {
                    MessageAppended?.Invoke("Checksum matched!");
                    Status = UploadStatus.Completed;
                }
                else
                {
                    MessageAppended?.Invoke("Failed: Checksum not matched");
                    Status = UploadStatus.ErrorNeedRestart;
                }
            }
            private async Task StartUploadAsync(long position)
            {
                Status = UploadStatus.Uploading;
                long chunkSize = MinChunkSize;
                for (; position != fileStream.Length;)
                {
                    var bufferSize = Math.Min(chunkSize, fileStream.Length - position);
                    byte[] buffer = new byte[bufferSize];
                    int actualLength = 0;
                    while (actualLength < Math.Min(bufferSize, MinChunkSize))
                    {
                        fileStream.Position = position + actualLength;
                        actualLength += await fileStream.ReadAsync(buffer, actualLength, (int)bufferSize - actualLength);
                    }
                    Array.Resize<byte>(ref buffer, actualLength);
                    DateTime startTime = DateTime.Now;
                    await DoResumableUploadAsync(position, buffer);
                    OnProgressChanged(position = byteReceivedSoFar, fileStream.Length);
                    if (Status == UploadStatus.Completed) return;
                    else if (Status != UploadStatus.Uploading)
                    {
                        MessageAppended?.Invoke($"Failed! Chunk range: {position}-{position + buffer.Length - 1}");
                        return;
                    }
                    if ((DateTime.Now - startTime).TotalSeconds < 0.2) chunkSize += chunkSize / 2;
                    else chunkSize = Math.Max(MinChunkSize, chunkSize / 2);
                    //MyLogger.Log($"Sent: {i + chunkSize}/{fileStream.Length} bytes");
                    if(pauseRequest)
                    {
                        Status = UploadStatus.Paused;
                        return;
                    }
                }
            }
            private async Task CreateResumableUploadAsync(IList<string> parents)
            {
                Status = UploadStatus.Creating;
                string json = $"{{\"name\":\"{fileName}\"";
                if (parents.Count > 0)
                {
                    json += ",\"parents\":[";
                    foreach (string parent in parents) json += $"\"{parent}\",";
                    json = json.Remove(json.Length - 1) + "]";
                }
                json += "}";
                MessageAppended?.Invoke(json);
                long totalBytes = fileStream.Length;
                HttpWebRequest request = WebRequest.CreateHttp("https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable");
                var bytes = Encoding.UTF8.GetBytes(json);
                request.Headers["Content-Type"] = "application /json; charset=UTF-8";
                request.Headers["Content-Length"] = bytes.Length.ToString(); // Convert.FromBase64String(json).Length.ToString();
                //request.Headers["X-Upload-Content-Type"]= Constants.GetMimeType(System.IO.Path.GetExtension(filePath));
                request.Headers["X-Upload-Content-Length"] = totalBytes.ToString();
                request.Headers["Authorization"] = "Bearer " + (await Drive.GetAccessTokenAsync());
                request.Method = "POST";
                using (System.IO.Stream requestStream = await request.GetRequestStreamAsync())
                {
                    await requestStream.WriteAsync(bytes, 0, bytes.Length);
                    //using (System.IO.StreamWriter streamWriter = new System.IO.StreamWriter(requestStream))
                    //{
                    //    await streamWriter.WriteAsync(json);
                    //}
                }
                using (var response = await GetHttpResponseAsync(request))
                {
                    if (response == null)
                    {
                        MessageAppended?.Invoke("Null response");
                        Status = UploadStatus.ErrorNeedRestart;
                        return;
                    }
                    MessageAppended?.Invoke($"Http response: {response.StatusCode} ({(int)response.StatusCode})");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        if (Array.IndexOf(response.Headers.AllKeys, "location") == -1)
                        {
                            MessageAppended?.Invoke("response.Headers doesn't contain a key for \"location\"!");
                            Status = UploadStatus.ErrorNeedRestart;
                            return;
                        }
                        var resumableUri = JsonConvert.SerializeObject(response.Headers["location"]);
                        MessageAppended?.Invoke($"Resumable Uri: {resumableUri}");
                        this.resumableUri = resumableUri;
                        Status = UploadStatus.Created;
                        return;
                    }
                    else
                    {
                        MessageAppended?.Invoke("Http response isn't OK!");
                        MessageAppended?.Invoke(await LogHttpWebResponse(response, true));
                        Status = UploadStatus.ErrorNeedRestart;
                        return;
                    }
                }
            }
            private async Task DoResumableUploadAsync(long startByte, byte[] dataBytes)
            {
                var request = WebRequest.CreateHttp(resumableUri);
                request.Headers["Content-Length"] = dataBytes.Length.ToString();
                request.Headers["Content-Range"] = $"bytes {startByte}-{startByte + dataBytes.Length - 1}/{fileStream.Length}";
                request.Method = "PUT";
                using (System.IO.Stream requestStream = await request.GetRequestStreamAsync())
                {
                    await requestStream.WriteAsync(dataBytes, 0, dataBytes.Length);
                }
                using (var response = await GetHttpResponseAsync(request))
                {
                    if (response == null)
                    {
                        Status = UploadStatus.ErrorNeedResume;
                        return;
                    }
                    if ((int)response.StatusCode == 308)
                    {
                        if (Array.IndexOf(response.Headers.AllKeys, "range") == -1)
                        {
                            MessageAppended?.Invoke("No bytes have been received by the server yet, starting from the first byte...");
                            byteReceivedSoFar = 0;
                        }
                        else
                        {
                            //bytes=0-42
                            string s = response.Headers["range"];
                            //MyLogger.Log($"Range received by server: {s}");
                            string pattern = "bytes=0-";
                            MyLogger.Assert(s.StartsWith(pattern));
                            byteReceivedSoFar = long.Parse(s.Substring(pattern.Length))+1;
                        }
                        return;
                    }
                    else if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                    {
                        using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                        {
                            var data = await reader.ReadToEndAsync();
                            //MyLogger.Log($"{data}");
                            var jsonObject = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(data);
                            MyLogger.Assert(fileName == (string)jsonObject["name"]);
                            CloudFileId = (string)jsonObject["id"];
                        }
                        byteReceivedSoFar = fileStream.Length;
                        await VerifyCheckSum();
                        return;
                    }
                    else
                    {
                        MessageAppended?.Invoke("Http response isn't 308!");
                        MessageAppended?.Invoke(await LogHttpWebResponse(response, true));
                        Status = UploadStatus.ErrorNeedResume;
                        return;
                    }
                }
            }
            #endregion
            #region PublicMethods
            public async Task UploadAsync()
            {
                switch (Status)
                {
                    case UploadStatus.ErrorNeedResume:
                    case UploadStatus.Paused:
                        {
                            MyLogger.Assert(resumableUri != null);
                            //indexRetry:;
                            MessageAppended?.Invoke($"Resuming... Uri: {resumableUri}");
                            var request = WebRequest.CreateHttp(resumableUri);
                            request.Headers["Content-Length"] = "0";
                            request.Headers["Content-Range"] = $"bytes */{fileStream.Length}";
                            request.Method = "PUT";
                            using (var response = await GetHttpResponseAsync(request))
                            {
                                if (response == null)
                                {
                                    MessageAppended?.Invoke("Null response");
                                    Status = UploadStatus.ErrorNeedResume;
                                    return;
                                }
                                MessageAppended?.Invoke($"Http response: {response.StatusCode} ({response.StatusCode})");
                                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                                {
                                    MessageAppended?.Invoke("The upload was already completed");
                                    MessageAppended?.Invoke(await LogHttpWebResponse(response, false));
                                    using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                                    {
                                        var data = await reader.ReadToEndAsync();
                                        var jsonObject = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(data);
                                        MyLogger.Assert(fileName == (string)jsonObject["name"]);
                                        CloudFileId = (string)jsonObject["id"];
                                    }
                                    response.Dispose();
                                    await VerifyCheckSum();
                                    return;
                                }
                                else if ((int)response.StatusCode == 308)
                                {
                                    long position;
                                    if (Array.IndexOf(response.Headers.AllKeys, "range") == -1)
                                    {
                                        MessageAppended?.Invoke("No bytes have been received by the server yet, starting from the first byte...");
                                        position = 0;
                                    }
                                    else
                                    {
                                        //bytes=0-42
                                        string s = response.Headers["range"];
                                        MessageAppended?.Invoke($"Range received by server: {s}");
                                        string pattern = "bytes=0-";
                                        MyLogger.Assert(s.StartsWith(pattern));
                                        position = long.Parse(s.Substring(pattern.Length));
                                    }
                                    response.Dispose();
                                    await StartUploadAsync(position);
                                    return;
                                }
                                else if (response.StatusCode == HttpStatusCode.NotFound)
                                {
                                    MessageAppended?.Invoke("The upload session has expired and the upload needs to be restarted from the beginning.");
                                    MessageAppended?.Invoke(await LogHttpWebResponse(response, true));
                                    response.Dispose();
                                    Status = UploadStatus.ErrorNeedRestart;
                                    return;
                                }
                                else
                                {
                                    MessageAppended?.Invoke("Http response isn't OK, Created or 308!");
                                    MessageAppended?.Invoke(await LogHttpWebResponse(response, true));
                                    response.Dispose();
                                    Status = UploadStatus.ErrorNeedResume;
                                    return;
                                }
                            }
                        }
                    case UploadStatus.NotStarted:
                        {
                            await CreateResumableUploadAsync(parents);
                            if (Status != UploadStatus.Created) return;
                            MyLogger.Assert(resumableUri.StartsWith("\"") && resumableUri.EndsWith("\""));
                            resumableUri = resumableUri.Substring(1, resumableUri.Length - 2);
                            await StartUploadAsync(0);
                            return;
                        }
                    case UploadStatus.Completed:
                    case UploadStatus.Created:
                    case UploadStatus.Creating:
                    case UploadStatus.ErrorNeedRestart:
                    case UploadStatus.Uploading:
                    default:throw new Exception($"Status: {Status}");
                }
            }
            public async Task PauseAsync()
            {
                switch (Status)
                {
                    case UploadStatus.Uploading:
                        {
                            pauseRequest = true;
                            MessageAppended?.Invoke("Pausing...");
                            while (Status == UploadStatus.Uploading) await Task.Delay(100);
                            MessageAppended?.Invoke("Paused");
                            pauseRequest = false;
                            return;
                        }
                    case UploadStatus.Completed:
                    case UploadStatus.ErrorNeedRestart:
                    case UploadStatus.ErrorNeedResume:
                    case UploadStatus.NotStarted:
                    case UploadStatus.Paused:
                        {
                            MyLogger.Log($"Status: {Status}, no action take to pause");
                            return;
                        }
                    default:
                        {
                            throw new Exception($"Status: {Status}");
                        }
                }
            }
            public Uploader(IList<string> _parents, System.IO.Stream _fileStream, string _fileName)
            {
                parents = _parents;
                fileStream = _fileStream;
                fileName = _fileName;
            }
            #endregion
            #region ProgressChangedEvent
            public event ProgressChangedEventHandler ProgressChanged;
            private void OnProgressChanged(long bytesSent, long totalLength) { ProgressChanged?.Invoke(bytesSent, totalLength); }
            #endregion
        }
        public class Downloader
        {
            System.IO.Stream fileStream = null,serverStream=null;
            long bytesReceivedSoFar = 0, serverStreamLength;
            string resumableUri = null;
            bool pauseRequest = false;
            public enum DownloadStatus {NotStarted,Downloading,Completed,Paused,ErrorNeedRestart,ErrorNeedResume };
            public DownloadStatus Status
            {
                get;
                private set;
            } = DownloadStatus.NotStarted;
            public event MessageAppendedEventHandler MessageAppended;
            private async Task<bool>CreateResumableDownloadAsync()
            {
                indexRetry:;
                if (serverStream != null)
                {
                    serverStream.Dispose();
                    serverStream = null;
                }
                var request = WebRequest.CreateHttp(resumableUri);
                request.Headers["Authorization"] = "Bearer " + (await Drive.GetAccessTokenAsync());
                request.Headers["Range"] = $"bytes={bytesReceivedSoFar}-";
                request.Method = "GET";
                var response = await GetHttpResponseAsync(request);
                if (response == null)
                {
                    MyLogger.Log("Null response, trying to create the download again...");
                    goto indexRetry;
                }
                else if (response.StatusCode == HttpStatusCode.PartialContent)
                {
                    serverStream = response.GetResponseStream();
                    if(Array.IndexOf(response.Headers.AllKeys, "content-length") ==-1)
                    {
                        MyLogger.Log($"Response header doesn't exist: content-length");
                        MyLogger.Log(await LogHttpWebResponse(response,true));
                        return false;
                    }
                    if(!long.TryParse(response.Headers["content-length"], out serverStreamLength))
                    {
                        MyLogger.Log($"Error parsing serverStreamLength from \"content-length\" header: {response.Headers["content-length"]}");
                        MyLogger.Log(await LogHttpWebResponse(response, true));
                        return false;
                    }
                    return true;
                }
                else
                {
                    MyLogger.Log("Http response isn't PartialContent!");
                    MyLogger.Log(await LogHttpWebResponse(response, true));
                    return false;
                }
            }
            private async Task<long> DoResumableDownloadAsync(int bufferSize)
            {
                try
                {
                    var buffer = new byte[bufferSize];
                    var bytesRead = await serverStream.ReadAsync(buffer, 0, bufferSize);
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    await fileStream.FlushAsync();
                    return bytesRead;
                }
                catch(Exception error)
                {
                    MessageAppended?.Invoke(error.ToString());
                    Status = DownloadStatus.ErrorNeedResume;
                    return -1;
                }
            }
            private async Task StartDownloadAsync()
            {
                Status = DownloadStatus.Downloading;
                MyLogger.Assert(fileStream != null && resumableUri != null);
                try
                {
                    if (!await CreateResumableDownloadAsync()) { Status = DownloadStatus.ErrorNeedRestart; MessageAppended?.Invoke("Error create resumable download async"); return; }
                    var fileSize = bytesReceivedSoFar + serverStreamLength;
                    MyLogger.Log($"File size = {fileSize}");
                    fileStream.Position = bytesReceivedSoFar;
                    int chunkSize = MinChunkSize;
                    for (; bytesReceivedSoFar != fileSize;)
                    {
                        DateTime startTime = DateTime.Now;
                        var bufferSize = (int)Math.Min(chunkSize, fileSize - bytesReceivedSoFar);
                        if (pauseRequest)
                        {
                            Status = DownloadStatus.Paused;
                            return;
                        }
                        var bytesRead = await DoResumableDownloadAsync(bufferSize);
                        if (Status == DownloadStatus.ErrorNeedResume)
                        {
                            MessageAppended?.Invoke($"Failed! Chunk range: {bytesReceivedSoFar}-{bytesReceivedSoFar + bufferSize - 1}");
                            return;
                        }
                        MyLogger.Assert(Status == DownloadStatus.Downloading && bytesRead != -1);
                        bytesReceivedSoFar += bytesRead;
                        if ((DateTime.Now - startTime).TotalSeconds < 0.2) chunkSize = (int)Math.Min((long)chunkSize + chunkSize / 2, int.MaxValue);
                        else chunkSize = Math.Max(MinChunkSize, chunkSize / 2);
                        OnProgressChanged(bytesReceivedSoFar, fileSize);
                    }
                    OnProgressChanged(fileSize, fileSize);
                    Status = DownloadStatus.Completed;
                }
                catch (Exception error)
                {
                    Status = DownloadStatus.ErrorNeedResume;
                    MessageAppended?.Invoke(error.ToString());
                }
            }
            public async Task DownloadAsync()
            {
                switch (Status)
                {
                    case DownloadStatus.Paused:
                    case DownloadStatus.ErrorNeedResume:
                    case DownloadStatus.NotStarted:
                        {
                            await StartDownloadAsync();
                        }break;
                    case DownloadStatus.ErrorNeedRestart:
                    case DownloadStatus.Downloading:
                    case DownloadStatus.Completed:
                    default:
                        {
                            throw new Exception($"Status: {Status}");
                        }
                }
            }
            public async Task PauseAsync()
            {
                switch(Status)
                {
                    case DownloadStatus.Downloading:
                        {
                            pauseRequest = true;
                            MessageAppended?.Invoke("Pausing...");
                            while (Status == DownloadStatus.Downloading) await Task.Delay(100);
                            MessageAppended?.Invoke("Paused");
                            pauseRequest = false;
                            return;
                        }
                    case DownloadStatus.Completed:
                    case DownloadStatus.ErrorNeedRestart:
                    case DownloadStatus.ErrorNeedResume:
                    case DownloadStatus.NotStarted:
                    case DownloadStatus.Paused:
                    default:
                        {
                            throw new Exception($"Status: {Status}");
                        }
                }
            }
            public Downloader(string fileId, System.IO.Stream _fileStream)
            {
                resumableUri = $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media";
                fileStream = _fileStream;
            }
            public event ProgressChangedEventHandler ProgressChanged;
            private void OnProgressChanged(long bytesGot, long totalLength) { ProgressChanged?.Invoke(bytesGot, totalLength); }
        }
    }
}
