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
