using System;
using System.Threading.Tasks;
using System.Net;

namespace GoogleDrive
{
    partial class RestRequests
    {
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
