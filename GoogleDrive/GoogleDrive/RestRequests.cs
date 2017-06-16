using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using Google.Apis.Auth.OAuth2;
using System.Net;
using Newtonsoft.Json;

namespace GoogleDrive
{
    class RestRequests
    {
        public class Uploader
        {
            //resumable upload REST API: https://developers.google.com/drive/v3/web/resumable-upload
            private async Task<HttpWebResponse> GetHttpResponseAsync(HttpWebRequest request)
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
                    if (await MyLogger.Ask("null response, try again?"))
                    {
                        goto indexRetry;
                    }
                    else return ans;
                }
                if (ans.StatusCode == HttpStatusCode.InternalServerError ||
                    ans.StatusCode == HttpStatusCode.BadGateway ||
                    ans.StatusCode == HttpStatusCode.ServiceUnavailable ||
                    ans.StatusCode == HttpStatusCode.GatewayTimeout)
                {
                    if(minisecs>500*16)
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
                else return ans;
            }
            private async Task<string> CreateResumableUploadAsync(DriveService driveService, IList<string> parents)
            {
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
                request.Headers["Content-Type"] = "application /json; charset=UTF-8";
                request.Headers["Content-Length"] = json.Length.ToString();
                //request.Headers["X-Upload-Content-Type"]= Constants.GetMimeType(System.IO.Path.GetExtension(filePath));
                request.Headers["X-Upload-Content-Length"] = totalBytes.ToString();
                request.Headers["Authorization"] = "Bearer " + (driveService.HttpClientInitializer as UserCredential).Token.AccessToken;
                request.Method = "POST";
                using (System.IO.Stream requestStream = await request.GetRequestStreamAsync())
                {
                    using (System.IO.StreamWriter streamWriter = new System.IO.StreamWriter(requestStream))
                    {
                        await streamWriter.WriteAsync(json);
                    }
                }
                using (var response = await GetHttpResponseAsync(request))
                {
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
                        return null;
                    }
                }
            }
            const int chunkSize = 262144 * 2;
            long byteReceivedSoFar;
            private async Task<bool> DoResumableUploadAsync(long startByte, byte[] dataBytes)
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
                        return true;
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
                        //StringBuilder sb = new StringBuilder();
                        //foreach (var key in response.Headers.AllKeys) sb.AppendLine($"{key}:{JsonConvert.SerializeObject(response.Headers[key])}");
                        //await MyLogger.Alert(sb.ToString());
                        //MyLogger.Assert(Array.IndexOf(response.Headers.AllKeys, "content-length") != -1);
                        byteReceivedSoFar = fileStream.Length;// long.Parse(response.Headers["content-length"]);
                        return true;
                    }
                    else
                    {
                        MyLogger.Log($"Http response: {response.StatusCode} ({(int)response.StatusCode})");
                        MyLogger.Log("Http response isn't 308!");
                        return false;
                    }
                }
            }
            class CloudFileJsonObject
            {
                public string kind, id, name, mimeType;
            }
            public async Task<string> ResumeUploadAsync()
            {
                var request = WebRequest.CreateHttp(resumableUri);
                request.Headers["Content-Length"] = "0";
                request.Headers["Content-Range"] = $"bytes */{fileStream.Length}";
                request.Method = "PUT";
                var response = await GetHttpResponseAsync(request);
                {
                    MyLogger.Log($"Http response: {response.StatusCode} ({response.StatusCode})");
                    if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                    {
                        MyLogger.Log("The upload was already completed");
                        //MyLogger.Log(JsonConvert.SerializeObject(response.Headers.AllKeys));
                        //foreach(var key in response.Headers.AllKeys)
                        //{
                        //    MyLogger.Log($"{key}:{response.Headers[key]}");
                        //}
                        using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                        {
                            var data = await reader.ReadToEndAsync();
                            //MyLogger.Log($"{data}");
                            var jsonObject = JsonConvert.DeserializeObject<CloudFileJsonObject>(data);
                            MyLogger.Assert(fileName == jsonObject.name);
                            result = jsonObject.id;
                        }
                        response.Dispose();
                        return result;
                    }
                    else if ((int)response.StatusCode == 308)
                    {
                        //MyLogger.Log(JsonConvert.SerializeObject(response.Headers.AllKeys));
                        //foreach (var key in response.Headers.AllKeys)
                        //{
                        //    MyLogger.Log($"{key}:{response.Headers[key]}");
                        //}
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
                        response.Dispose();
                        return null;
                    }
                    else
                    {
                        MyLogger.Log("Http response isn't OK, Created or 308!");
                        response.Dispose();
                        return null;
                    }
                }
            }
            private async Task<string> UploadAsync(long position)
            {
                try
                {
                    fileStream.Position = position;
                    for (; position != fileStream.Length + 1;)
                    {
                        var bufferSize = Math.Min(chunkSize, fileStream.Length - position);
                        byte[] buffer = new byte[bufferSize];
                        await fileStream.ReadAsync(buffer, 0, (int)bufferSize);
                        if (!await DoResumableUploadAsync(position, buffer))
                        {
                            MyLogger.Log($"Failed! Chunk range: {position}-{position + bufferSize - 1}");
                            return null;
                        }
                        //MyLogger.Log($"Sent: {i + chunkSize}/{fileStream.Length} bytes");
                        OnProgressChanged(position = byteReceivedSoFar + 1, fileStream.Length);
                    }
                    OnProgressChanged(fileStream.Length, fileStream.Length);
                    fileStream.Dispose();
                    return result;
                }
                catch (Exception error)
                {
                    await MyLogger.Alert(error.ToString());
                    return null;
                }
            }
            public async Task<string> UploadAsync(DriveService driveService, IList<string> parents, System.IO.Stream _fileStream, string _fileName)
            {
                try
                {
                    fileStream = _fileStream;
                    fileName = _fileName;
                    resumableUri = await CreateResumableUploadAsync(driveService, parents);
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
            System.IO.Stream fileStream;
            string result, resumableUri,fileName;
            public delegate void ProgressChangedEventHandler(long bytesSent, long totalLength);
            public event ProgressChangedEventHandler ProgressChanged;
            private void OnProgressChanged(long bytesSent, long totalLength) { ProgressChanged?.Invoke(bytesSent, totalLength); }
            //private async Task<bool> CompleteResumableUploadAsync(long startByte, byte[] dataBytes)
            //{
            //    var request = WebRequest.CreateHttp(resumableUri);
            //    request.Headers["Content-Length"] = dataBytes.Length.ToString();
            //    request.Headers["Content-Range"] = $"bytes {startByte}-{startByte + dataBytes.Length - 1}/{fileStream.Length}";
            //    request.Method = "PUT";
            //    using (System.IO.Stream requestStream = await request.GetRequestStreamAsync())
            //    {
            //        await requestStream.WriteAsync(dataBytes, 0, dataBytes.Length);
            //    }
            //    using (var response = await GetHttpResponseAsync(request))
            //    {
            //        MyLogger.Log($"Http response: {response.StatusCode} ({(int)response.StatusCode})");
            //        if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
            //        {
            //            //MyLogger.Log(JsonConvert.SerializeObject(response.Headers.AllKeys));
            //            //foreach(var key in response.Headers.AllKeys)
            //            //{
            //            //    MyLogger.Log($"{key}:{response.Headers[key]}");
            //            //}
            //            using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
            //            {
            //                var data = await reader.ReadToEndAsync();
            //                //MyLogger.Log($"{data}");
            //                var jsonObject = JsonConvert.DeserializeObject<CloudFileJsonObject>(data);
            //                MyLogger.Assert(System.IO.Path.GetFileName(filePath) == jsonObject.name);
            //                result = jsonObject.id;
            //            }
            //            return true;
            //        }
            //        else
            //        {
            //            MyLogger.Log("Http response isn't OK or Created!");
            //            return false;
            //        }
            //    }
            //}
        }
    }
}
