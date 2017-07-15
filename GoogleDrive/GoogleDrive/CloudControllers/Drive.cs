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
        private static UserCredential _credential = null;
        private static async Task AuthorizeAsync()
        {
            Log("Authorizing...");
            //GoogleWebAuthorizationBroker.Folder = "Drive.Sample";
            _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    new Uri("ms-appx:///Assets/client_id.json"),
                    new[] { DriveService.Scope.DriveFile, DriveService.Scope.Drive },
                    "user",
                    CancellationToken.None);
            Log("Creating service...");
            // Create the service.
            if (_driveService != null) _driveService.Dispose();
            _driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _credential,
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
            Log("Reauthorizing...");
            await GoogleWebAuthorizationBroker.ReauthorizeAsync(_credential, CancellationToken.None);
            Log("Refreshing token...");
            while (!(await _credential.RefreshTokenAsync(CancellationToken.None))) Log("Failed to refresh token, retrying...");
            Log("Creating service...");
            MyLogger.Assert(_driveService != null);
            _driveService.Dispose();
            _driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _credential,
                ApplicationName = "Google Drive APIs",
            });
            Log("Service created!");
            MyLogger.Log($"Access token: {await GetAccessTokenAsync()}");
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
