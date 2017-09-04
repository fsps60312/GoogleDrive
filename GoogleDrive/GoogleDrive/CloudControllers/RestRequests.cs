using System;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;

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
        public static async Task<string> LogHttpWebResponsePublic(HttpWebResponse response, bool readStream)
        {
            return await LogHttpWebResponse(response, readStream);
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
        public static async Task<HttpWebResponse> GetHttpResponsePublicAsync(HttpWebRequest request)
        {
            return await GetHttpResponseAsync(request);
        }
        public delegate void ProgressChangedEventHandler(long bytesProcessed, long totalLength);
    }
}
