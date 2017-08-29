using System.Threading.Tasks;
using System.Net;

namespace GoogleDrive
{
    partial class RestRequests
    {
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
                    //MyLogger.Log($"Http response: {response.StatusCode} ({(int)response.StatusCode})");
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
    }
}
