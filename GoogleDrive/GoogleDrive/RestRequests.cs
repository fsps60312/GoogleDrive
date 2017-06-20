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
        private static void LogHttpWebResponse(HttpWebResponse response,bool readStream)
        {
            MyLogger.Log($"Http response: {response.StatusCode} ({(int)response.StatusCode})");
            StringBuilder sb = new StringBuilder();
            foreach (var key in response.Headers.AllKeys) sb.AppendLine($"{key}:{JsonConvert.SerializeObject(response.Headers[key])}");
            MyLogger.Log(sb.ToString());
            if (readStream)
            {
                var reader = new System.IO.StreamReader(response.GetResponseStream());
                MyLogger.Log(reader.ReadToEnd());
                reader.Dispose();
            }
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
                        LogHttpWebResponse(ans, true); ;
                        MyLogger.Assert(Array.IndexOf(request.Headers.AllKeys, "Authorization") != -1);
                        request.Headers["Authorization"] = "Bearer " + (await Drive.RefreshAccessTokenAsync());
                        goto indexRetry;
                    }
                default: return ans;
            }
        }
        public delegate void ProgressChangedEventHandler(long bytesProcessed, long totalLength);
        class CloudFileJsonObject
        {
            public string kind, id, name, mimeType;
        }
        public class Uploader
        {
            //resumable upload REST API: https://developers.google.com/drive/v3/web/resumable-upload
            #region Private Fields & Methods
            System.IO.Stream fileStream;
            string result, resumableUri = null, fileName;
            IList<string> parents;
            long byteReceivedSoFar;
            enum ChunkUploadResult { Success, Failed, NullResponse };
            private async Task<string> UploadAsync(long position)
            {
                try
                {
                    long chunkSize = MinChunkSize;
                    for (; position != fileStream.Length;)
                    {
                        var bufferSize = Math.Min(chunkSize, fileStream.Length - position);
                        byte[] buffer = new byte[bufferSize];
                        int actualLength = 0;
                        while(actualLength<Math.Min(bufferSize, MinChunkSize))
                        {
                            fileStream.Position = position + actualLength;
                            actualLength += await fileStream.ReadAsync(buffer, actualLength, (int)bufferSize - actualLength);
                        }
                        Array.Resize<byte>(ref buffer, actualLength);
                        DateTime startTime = DateTime.Now;
                        var result = await DoResumableUploadAsync(position, buffer);
                        switch (result)
                        {
                            case ChunkUploadResult.Success: break;
                            case ChunkUploadResult.Failed:
                                {
                                    MyLogger.Log($"Failed! Chunk range: {position}-{position + buffer.Length - 1}");
                                    return null;
                                }
                            case ChunkUploadResult.NullResponse:
                                {
                                    MyLogger.Log("Null response, trying to resume...");
                                    return await ResumeUploadAsync();
                                }
                            default:
                                {
                                    MyLogger.Log($"Unknown ChunkUploadResult: {result}");
                                    MyLogger.Assert(false);
                                    return null;
                                }
                        }
                        if ((DateTime.Now - startTime).TotalSeconds < 0.2) chunkSize += chunkSize / 2;
                        else chunkSize = Math.Max(MinChunkSize, chunkSize / 2);
                        //MyLogger.Log($"Sent: {i + chunkSize}/{fileStream.Length} bytes");
                        OnProgressChanged(position = byteReceivedSoFar + 1, fileStream.Length);
                    }
                    OnProgressChanged(fileStream.Length, fileStream.Length);
                    return result;
                }
                catch (Exception error)
                {
                    await MyLogger.Alert(error.ToString());
                    return null;
                }
            }
            private async Task<string> CreateResumableUploadAsync(IList<string> parents)
            {
                indexRetry:;
                string json = $"{{\"name\":\"{fileName}\"";
                if (parents.Count > 0)
                {
                    json += ",\"parents\":[";
                    foreach (string parent in parents) json += $"\"{parent}\",";
                    json = json.Remove(json.Length - 1) + "]";
                }
                json += "}";
                MyLogger.Log(json);
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
                        MyLogger.Log("Null response, trying to create the upload again...");
                        goto indexRetry;
                    }
                    MyLogger.Log($"Http response: {response.StatusCode} ({(int)response.StatusCode})");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        if (Array.IndexOf(response.Headers.AllKeys, "location") == -1)
                        {
                            MyLogger.Log("response.Headers doesn't contain a key for \"location\"!");
                            return null;
                        }
                        var resumableUri = JsonConvert.SerializeObject(response.Headers["location"]);
                        MyLogger.Log($"Resumable Uri: {resumableUri}");
                        return resumableUri;
                    }
                    else
                    {
                        MyLogger.Log("Http response isn't OK!");
                        LogHttpWebResponse(response,true);
                        return null;
                    }
                }
            }
            private async Task<ChunkUploadResult> DoResumableUploadAsync(long startByte, byte[] dataBytes)
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
                    if (response == null) return ChunkUploadResult.NullResponse;
                    if ((int)response.StatusCode == 308)
                    {
                        if (Array.IndexOf(response.Headers.AllKeys, "range") == -1)
                        {
                            MyLogger.Log("No bytes have been received by the server yet, starting from the first byte...");
                            byteReceivedSoFar = 0;
                        }
                        else
                        {
                            //bytes=0-42
                            string s = response.Headers["range"];
                            //MyLogger.Log($"Range received by server: {s}");
                            string pattern = "bytes=0-";
                            MyLogger.Assert(s.StartsWith(pattern));
                            byteReceivedSoFar = long.Parse(s.Substring(pattern.Length));
                        }
                        return ChunkUploadResult.Success;
                    }
                    else if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                    {
                        using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                        {
                            var data = await reader.ReadToEndAsync();
                            //MyLogger.Log($"{data}");
                            var jsonObject = JsonConvert.DeserializeObject<CloudFileJsonObject>(data);
                            MyLogger.Assert(fileName == jsonObject.name);
                            result = jsonObject.id;
                        }
                        byteReceivedSoFar = fileStream.Length-1;
                        return ChunkUploadResult.Success;
                    }
                    else
                    {
                        MyLogger.Log("Http response isn't 308!");
                        LogHttpWebResponse(response,true);
                        return ChunkUploadResult.Failed;
                    }
                }
            }
            #endregion
            #region PublicMethods
            public async Task<string> ResumeUploadAsync()
            {
                MyLogger.Assert(resumableUri != null);
                indexRetry:;
                MyLogger.Log($"Resuming... Uri: {resumableUri}");
                var request = WebRequest.CreateHttp(resumableUri);
                request.Headers["Content-Length"] = "0";
                request.Headers["Content-Range"] = $"bytes */{fileStream.Length}";
                request.Method = "PUT";
                using (var response = await GetHttpResponseAsync(request))
                {
                    if (response == null)
                    {
                        MyLogger.Log("Null response, trying to resume the upload again...");
                        goto indexRetry;
                    }
                    try
                    {
                        MyLogger.Log($"Http response: {response.StatusCode} ({response.StatusCode})");
                        if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                        {
                            MyLogger.Log("The upload was already completed");
                            LogHttpWebResponse(response, false);
                            using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                            {
                                var data = await reader.ReadToEndAsync();
                                var jsonObject = JsonConvert.DeserializeObject<CloudFileJsonObject>(data);
                                MyLogger.Assert(fileName == jsonObject.name);
                                result = jsonObject.id;
                            }
                            response.Dispose();
                            return result;
                        }
                        else if ((int)response.StatusCode == 308)
                        {
                            long position;
                            if (Array.IndexOf(response.Headers.AllKeys, "range") == -1)
                            {
                                MyLogger.Log("No bytes have been received by the server yet, starting from the first byte...");
                                position = 0;
                            }
                            else
                            {
                                //bytes=0-42
                                string s = response.Headers["range"];
                                MyLogger.Log($"Range received by server: {s}");
                                string pattern = "bytes=0-";
                                MyLogger.Assert(s.StartsWith(pattern));
                                position = long.Parse(s.Substring(pattern.Length));
                            }
                            response.Dispose();
                            return await UploadAsync(position);
                        }
                        else if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            MyLogger.Log("The upload session has expired and the upload needs to be restarted from the beginning.");
                            LogHttpWebResponse(response,true);
                            response.Dispose();
                            return null;
                        }
                        else
                        {
                            MyLogger.Log("Http response isn't OK, Created or 308!");
                            LogHttpWebResponse(response,true);
                            response.Dispose();
                            return null;
                        }
                    }
                    catch (Exception error)
                    {
                        if (await MyLogger.Ask($"Error when resuming the upload, try again?\r\n{error}")) goto indexRetry;
                        else return null;
                    }
                }
            }
            public async Task<string> UploadAsync(IList<string> _parents, System.IO.Stream _fileStream, string _fileName)
            {
                parents = _parents;
                fileStream = _fileStream;
                fileName = _fileName;
                try
                {
                    resumableUri = await CreateResumableUploadAsync(parents);
                    MyLogger.Assert(resumableUri.StartsWith("\"") && resumableUri.EndsWith("\""));
                    resumableUri = resumableUri.Substring(1, resumableUri.Length - 2);
                    return await UploadAsync(0);
                }
                catch (Exception error)
                {
                    await MyLogger.Alert(error.ToString());
                    return null;
                }
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
            long bytesReceivedSoFar,serverStreamLength;
            string resumableUri = null;
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
                        LogHttpWebResponse(response,true);
                        return false;
                    }
                    if(!long.TryParse(response.Headers["content-length"], out serverStreamLength))
                    {
                        MyLogger.Log($"Error parsing serverStreamLength from \"content-length\" header: {response.Headers["content-length"]}");
                        LogHttpWebResponse(response,true);
                        return false;
                    }
                    return true;
                }
                else
                {
                    MyLogger.Log("Http response isn't PartialContent!");
                    LogHttpWebResponse(response,true);
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
                    MyLogger.Log(error.ToString());
                    return -1;
                }
            }
            private async Task<bool> DownloadAsync()
            {
                MyLogger.Assert(fileStream != null);
                MyLogger.Assert(resumableUri != null);
                try
                {
                    if (!await CreateResumableDownloadAsync()) return false;
                    var fileSize = bytesReceivedSoFar+serverStreamLength;
                    MyLogger.Log($"File size = {fileSize}");
                    fileStream.Position = bytesReceivedSoFar;
                    int chunkSize = MinChunkSize;
                    for (; bytesReceivedSoFar != fileSize;)
                    {
                        DateTime startTime = DateTime.Now;
                        var bufferSize = (int)Math.Min(chunkSize, fileSize - bytesReceivedSoFar);
                        var bytesRead = await DoResumableDownloadAsync(bufferSize);
                        if (bytesRead == -1)
                        {
                            MyLogger.Log($"Failed! Chunk range: {bytesReceivedSoFar}-{bytesReceivedSoFar + bufferSize - 1}");
                            return false;
                        }
                        else bytesReceivedSoFar += bytesRead;
                        if ((DateTime.Now - startTime).TotalSeconds < 0.2) chunkSize = (int)Math.Min((long)chunkSize + chunkSize / 2, int.MaxValue);
                        else chunkSize = Math.Max(MinChunkSize, chunkSize / 2);
                        OnProgressChanged(bytesReceivedSoFar, fileSize);
                    }
                    OnProgressChanged(fileSize, fileSize);
                    return true;
                }
                catch (Exception error)
                {
                    await MyLogger.Alert(error.ToString());
                    return false;
                }
            }
            public async Task<bool> ResumeDownloadAsync()
            {
                MyLogger.Assert(resumableUri != null);
                MyLogger.Assert(fileStream != null);
                return await DownloadAsync();
            }
            //private async Task<long> GetFileSizeAsync(string fileId)
            //{
            //    var request = WebRequest.CreateHttp(resumableUri);
            //    request.Headers["Authorization"] = "Bearer " + (await Drive.GetAccessTokenAsync());
            //    request.Method = "GET";
            //    using (var response = await GetHttpResponseAsync(request))
            //    {
            //        if (response == null)
            //        {
            //            MyLogger.Log("null response");
            //        }
            //        else
            //        {
            //            LogHttpWebResponse(response, true);
            //        }
            //    }
            //    throw new NotImplementedException();
            //}
            public async Task<bool> DownloadAsync(string fileId, System.IO.Stream _fileStream)
            {
                resumableUri = $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media";
                fileStream = _fileStream;
                bytesReceivedSoFar = 0;
                return await DownloadAsync();
            }
            public event ProgressChangedEventHandler ProgressChanged;
            private void OnProgressChanged(long bytesGot, long totalLength) { ProgressChanged?.Invoke(bytesGot, totalLength); }
        }
        //public class Downloader
        //{
        //    System.IO.Stream fileStream = null;
        //    long bytesReceivedSoFar, fileSize;
        //    string resumableUri = null;
        //    enum ChunkDownloadResult { Success, Failed, NullResponse };
        //    private async Task<ChunkDownloadResult> DoResumableDownloadAsync(int bufferSize)
        //    {
        //        var request = WebRequest.CreateHttp(resumableUri);
        //        request.Headers["Authorization"] = "Bearer " + (await Drive.GetAccessTokenAsync());
        //        request.Headers["Range"] = $"bytes={bytesReceivedSoFar}-{bytesReceivedSoFar + bufferSize - 1}";
        //        request.Method = "GET";
        //        using (var response = await GetHttpResponseAsync(request))
        //        {
        //            if (response == null) return ChunkDownloadResult.NullResponse;
        //            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
        //            {
        //                byte[] buffer = new byte[bufferSize + 1];
        //                var bytesRead = await response.GetResponseStream().ReadAsync(buffer, 0, bufferSize + 1);
        //                if (bytesRead != bufferSize)
        //                {
        //                    MyLogger.Log($"Expect {bufferSize} bytes, {bytesRead} read");
        //                    return ChunkDownloadResult.Failed;
        //                }
        //                fileStream.Position = bytesReceivedSoFar;
        //                await fileStream.WriteAsync(buffer, 0, bufferSize);
        //                return ChunkDownloadResult.Success;
        //            }
        //            else
        //            {
        //                MyLogger.Log("Http response isn't OK!");
        //                LogHttpWebResponse(response);
        //                return ChunkDownloadResult.Failed;
        //            }
        //        }
        //    }
        //    private async Task<bool> DownloadAsync()
        //    {
        //        MyLogger.Assert(fileStream != null);
        //        MyLogger.Assert(resumableUri != null);
        //        MyLogger.Assert(bytesReceivedSoFar <= fileSize);
        //        if (bytesReceivedSoFar == fileSize)
        //        {
        //            MyLogger.Log("The download was already completed");
        //            return true;
        //        }
        //        try
        //        {
        //            fileStream.Position = bytesReceivedSoFar;
        //            int chunkSize = MinChunkSize;
        //            for (; bytesReceivedSoFar != fileSize;)
        //            {
        //                DateTime startTime = DateTime.Now;
        //                var bufferSize = (int)Math.Min(chunkSize, fileSize - bytesReceivedSoFar);
        //                var result = await DoResumableDownloadAsync(bufferSize);
        //                switch (result)
        //                {
        //                    case ChunkDownloadResult.Success:
        //                        {
        //                            bytesReceivedSoFar += bufferSize;
        //                            MyLogger.Assert(fileStream.Position == bytesReceivedSoFar - 1);
        //                            break;
        //                        }
        //                    case ChunkDownloadResult.Failed:
        //                        {
        //                            MyLogger.Log($"Failed! Chunk range: {bytesReceivedSoFar}-{bytesReceivedSoFar + bufferSize - 1}");
        //                            return false;
        //                        }
        //                    case ChunkDownloadResult.NullResponse:
        //                        {
        //                            MyLogger.Log("Null response, trying to resume...");
        //                            return await ResumeDownloadAsync();
        //                        }
        //                    default:
        //                        {
        //                            MyLogger.Log($"Unknown ChunkUploadResult: {result}");
        //                            MyLogger.Assert(false);
        //                            return false;
        //                        }
        //                }
        //                if ((DateTime.Now - startTime).TotalSeconds < 0.2) chunkSize = (int)Math.Min((long)chunkSize + chunkSize / 2, int.MaxValue);
        //                else chunkSize = Math.Max(MinChunkSize, chunkSize / 2);
        //                OnProgressChanged(bytesReceivedSoFar, fileSize);
        //            }
        //            OnProgressChanged(fileSize, fileSize);
        //            return true;
        //        }
        //        catch (Exception error)
        //        {
        //            await MyLogger.Alert(error.ToString());
        //            return false;
        //        }
        //    }
        //    public async Task<bool> ResumeDownloadAsync()
        //    {
        //        MyLogger.Assert(resumableUri != null);
        //        MyLogger.Assert(fileStream != null);
        //        return await DownloadAsync();
        //    }
        //    private async Task<long> GetFileSizeAsync(string fileId)
        //    {
        //        var request = WebRequest.CreateHttp(resumableUri);
        //        request.Headers["Authorization"] = "Bearer " + (await Drive.GetAccessTokenAsync());
        //        request.Method = "GET";
        //        using (var response = await GetHttpResponseAsync(request))
        //        {
        //            if (response == null)
        //            {
        //                MyLogger.Log("null response");
        //            }
        //            else
        //            {
        //                LogHttpWebResponse(response, true);
        //            }
        //        }
        //        throw new NotImplementedException();
        //    }
        //    public async Task<bool> DownloadAsync(string fileId, System.IO.Stream _fileStream)
        //    {
        //        resumableUri = $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media";
        //        fileStream = _fileStream;
        //        bytesReceivedSoFar = 0;
        //        fileSize = await GetFileSizeAsync(fileId);
        //        return await DownloadAsync();
        //    }
        //    public event ProgressChangedEventHandler ProgressChanged;
        //    private void OnProgressChanged(long bytesGot, long totalLength) { ProgressChanged?.Invoke(bytesGot, totalLength); }
        //}
    }
}
