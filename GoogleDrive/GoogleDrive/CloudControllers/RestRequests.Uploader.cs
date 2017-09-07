using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using System.Threading;

namespace GoogleDrive
{
    partial class RestRequests
    {
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
            private volatile SemaphoreSlim semaphoreSlim = new SemaphoreSlim(0, 1);
            //private async Task<UploadStatus> VerifyCheckSum()
            //{
            //    MessageAppended?.Invoke("Calculating checksum...");
            //    string cloudCheckSum = await Libraries.GetSha256ForCloudFileById(CloudFileId);
            //    MessageAppended?.Invoke($"Cloud: {cloudCheckSum}");
            //    fileStream.Position = 0;
            //    string localCheckSum = await Libraries.GetSha256ForWindowsStorageFile(fileStream);
            //    MessageAppended?.Invoke($"Local: {localCheckSum}");
            //    if (cloudCheckSum != null && cloudCheckSum == localCheckSum)
            //    {
            //        MessageAppended?.Invoke("Checksum matched!");
            //        return UploadStatus.Completed;
            //    }
            //    else
            //    {
            //        MessageAppended?.Invoke("Failed: Checksum not matched");
            //        return UploadStatus.ErrorNeedRestart;
            //    }
            //}
            private async Task StartUploadAsync(long position)
            {
                if (pauseRequest)
                {
                    Status = UploadStatus.Paused;
                    lock (semaphoreSlim) semaphoreSlim.Release();
                    return;
                }
                Status = UploadStatus.Uploading;
                long chunkSize = MinChunkSize;
                long bytesLeftStatistics = fileStream.Length - position;
                CloudFile.Networker.OnTotalAmountRemainChanged(bytesLeftStatistics);
                try
                {
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
                        if (Status == UploadStatus.Completed)
                        {
                            OnChunkSent(buffer.Length);
                            bytesLeftStatistics -= buffer.Length;
                            CloudFile.Networker.OnTotalAmountRemainChanged(-buffer.Length);
                            return;
                        }
                        else if (Status != UploadStatus.Uploading)
                        {
                            MessageAppended?.Invoke($"Failed! Chunk range: {position}-{position + buffer.Length - 1}");
                            return;
                        }
                        else
                        {
                            OnChunkSent(buffer.Length);
                            bytesLeftStatistics -= buffer.Length;
                            CloudFile.Networker.OnTotalAmountRemainChanged(-buffer.Length);
                        }
                        if ((DateTime.Now - startTime).TotalSeconds < 0.2) chunkSize += chunkSize / 2;
                        else chunkSize = Math.Max(MinChunkSize, chunkSize / 2);
                        //MyLogger.Log($"Sent: {i + chunkSize}/{fileStream.Length} bytes");
                        if (pauseRequest)
                        {
                            Status = UploadStatus.Paused;
                            lock (semaphoreSlim) semaphoreSlim.Release();
                            return;
                        }
                    }
                }
                finally
                {
                    CloudFile.Networker.OnTotalAmountRemainChanged(-bytesLeftStatistics);
                }
            }
            private async Task CreateResumableUploadAsync()
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
                        MyLogger.Log("b");
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
                        Status = UploadStatus.Completed;
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
            private string GetSeperateString(byte[]data)
            {
                return Libraries.GetNonsubstring_A_Z(data);
            }
            private Tuple<string,byte[]>MergeMetadataAndFileContent(byte[] metadata,byte[]fileContent)
            {
                var bytes = new byte[metadata.Length + fileContent.Length];
                Array.Copy(metadata, bytes, metadata.Length);
                Array.Copy(fileContent, 0, bytes, metadata.Length, fileContent.Length);
                var seperateString = GetSeperateString(bytes);
                var ans = new List<byte>();
                ans.AddRange(Encoding.UTF8.GetBytes($"--{seperateString}\n"));
                {
                    ans.AddRange(metadata);
                }
                ans.AddRange(Encoding.UTF8.GetBytes($"\n--{seperateString}\n"));
                {
                    ans.AddRange(Encoding.UTF8.GetBytes(/*"Content-Type: text/plain\n"+*/"\n"));
                    ans.AddRange(fileContent);
                }
                ans.AddRange(Encoding.UTF8.GetBytes($"\n--{seperateString}--"));
                //MyLogger.Log(Encoding.UTF8.GetString(ans.ToArray()));
                return new Tuple<string, byte[]>(seperateString, ans.ToArray());
            }
            private async Task DoMultipartUploadAsync()
            {
                Status = UploadStatus.Uploading;
                OnProgressChanged(0, fileStream.Length);
                CloudFile.Networker.OnTotalAmountRemainChanged(fileStream.Length);
                try
                {
                    var body = MergeMetadataAndFileContent(new Func<byte[]>(() =>
                    {
                        string json = $"Content-Type: application/json; charset=UTF-8\n\n{{\"name\":\"{fileName}\"";
                        if (parents.Count > 0)
                        {
                            json += ",\"parents\":[";
                            foreach (string parent in parents) json += $"\"{parent}\",";
                            json = json.Remove(json.Length - 1) + "]";
                        }
                        json += "}";
                        MessageAppended?.Invoke(json);
                        return Encoding.UTF8.GetBytes(json);
                    })(), await new Func<Task<byte[]>>(async () =>
                     {
                         byte[] buffer = new byte[fileStream.Length];
                         for (int i = 0; i < buffer.Length;)
                         {
                             i += await fileStream.ReadAsync(buffer, i, buffer.Length - i);
                         }
                         return buffer;
                     })());
                    //MessageAppended?.Invoke($"Request to send:\r\n{Encoding.UTF8.GetString(body.Item2)}");
                    //long totalBytes = fileStream.Length;
                    HttpWebRequest request = WebRequest.CreateHttp("https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart");
                    request.Headers["Content-Type"] = "multipart/related; charset=UTF-8; boundary=" + body.Item1;
                    request.Headers["Content-Length"] = body.Item2.Length.ToString(); // Convert.FromBase64String(json).Length.ToString();
                                                                                      //request.Headers["X-Upload-Content-Type"]= Constants.GetMimeType(System.IO.Path.GetExtension(filePath));
                                                                                      //request.Headers["X-Upload-Content-Length"] = totalBytes.ToString();
                    request.Headers["Authorization"] = "Bearer " + (await Drive.GetAccessTokenAsync());
                    request.Method = "POST";
                    using (System.IO.Stream requestStream = await request.GetRequestStreamAsync())
                    {
                        await requestStream.WriteAsync(body.Item2, 0, body.Item2.Length);
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
                            OnProgressChanged(fileStream.Length, fileStream.Length);
                            OnChunkSent(fileStream.Length);
                            Status = UploadStatus.Completed;
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
                finally
                {
                    CloudFile.Networker.OnTotalAmountRemainChanged(-fileStream.Length);
                }
            }
            #endregion
            #region PublicMethods
            public async Task UploadAsync()
            {
                try
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
                                        MyLogger.Log("c");
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
                                        Status = UploadStatus.Completed;
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
                                if (fileStream.Length < MinChunkSize)
                                {
                                    MessageAppended?.Invoke($"File too small ({fileStream.Length}), using multipart upload instead");
                                    await DoMultipartUploadAsync();
                                }
                                else
                                {
                                    await CreateResumableUploadAsync();
                                    if (Status != UploadStatus.Created) return;
                                    MyLogger.Assert(resumableUri.StartsWith("\"") && resumableUri.EndsWith("\""));
                                    resumableUri = resumableUri.Substring(1, resumableUri.Length - 2);
                                    await StartUploadAsync(0);
                                }
                                return;
                            }
                        case UploadStatus.Completed:
                        case UploadStatus.Created:
                        case UploadStatus.Creating:
                        case UploadStatus.ErrorNeedRestart:
                        case UploadStatus.Uploading:
                        default: throw new Exception($"Status: {Status}");
                    }
                }
                finally
                {
                    if (pauseRequest)
                    {
                        MyLogger.Assert(Status != UploadStatus.Uploading);
                        lock (semaphoreSlim) semaphoreSlim.Release();
                    }
                }
            }
            public async Task PauseAsync()
            {
                switch (Status)
                {
                    case UploadStatus.Uploading:
                    case UploadStatus.Creating:
                    case UploadStatus.Created:
                        {
                            MessageAppended?.Invoke("Pausing...");
                            pauseRequest = true;
                            await semaphoreSlim.WaitAsync(10000);
                            //MyLogger.Assert(Status != UploadStatus.Uploading);
                            //while (Status == UploadStatus.Uploading) await Task.Delay(100);
                            pauseRequest = false;
                            if(Status == UploadStatus.Uploading)
                            {
                                var msg = $"Status: {Status}, failed to pause";
                                MyLogger.Log(msg);
                                MessageAppended?.Invoke(msg);
                            }
                            //MessageAppended?.Invoke($"Status after paused: {Status}");
                            //if () await MyLogger.Alert($"Status after paused: {Status}");
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
            public event ChunkSentEventHandler ChunkSent;
            private void OnChunkSent(long coda) { ChunkSent?.Invoke(coda); }
        }
    }
}
