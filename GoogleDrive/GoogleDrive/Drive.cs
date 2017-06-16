using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;

namespace GoogleDrive
{
    class Drive
    {
        private static DriveService _driveService = null;
        private static async Task AuthorizeAsync()
        {
            Log("Authorizing...");
            //GoogleWebAuthorizationBroker.Folder = "Drive.Sample";
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    new Uri("ms-appx:///Assets/client_id.json"),
                    new[] { DriveService.Scope.DriveFile, DriveService.Scope.Drive },
                    "user",
                    CancellationToken.None);
            Log("Creating service...");
            // Create the service.
            if (_driveService != null) _driveService.Dispose();
            _driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Google Drive APIs",
            });
            Log("Service created!");
        }
        private static void Log(string log) { MyLogger.Log(log); }
        public static async Task<string> GetAccessTokenAsync()
        {
            var driveService = await GetDriveServiceAsync();
            return (driveService.HttpClientInitializer as UserCredential).Token.AccessToken;
        }
        public static async Task<string>RefreshAccessTokenAsync()
        {
            MyLogger.Assert(_driveService != null);
            _driveService.Dispose();
            _driveService = null;
            return await GetAccessTokenAsync();
        }
        public static async Task<DriveService> GetDriveServiceAsync()
        {
            if (_driveService == null)
            {
                await AuthorizeAsync();
                MyLogger.Log($"Access token: {await GetAccessTokenAsync()}");
            }
            return _driveService;
        }
    }
}
