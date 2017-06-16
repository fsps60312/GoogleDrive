using System;
using System.Collections.Generic;
using System.Text;

namespace GoogleDrive
{
    static class Constants
    {
        /// <summary>
        /// 自動判斷檔案的 MimeType
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string GetMimeType(string extension)
        {
            return null;
            //string mimeType = "application/unknown";
            //Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(extension.ToLower());
            //if (regKey != null && regKey.GetValue("Content Type") != null)
            //    mimeType = regKey.GetValue("Content Type").ToString();
            //return mimeType;
        }
        public readonly static string FolderMimeType = "application/vnd.google-apps.folder";
        public static string Json = "{\"installed\":{\"client_id\":\"767856013993-4jsq7q0iujolnd9bomvd36ff4md16rv7.apps.googleusercontent.com\",\"project_id\":\"valiant-pager-169009\",\"auth_uri\":\"https://accounts.google.com/o/oauth2/auth\",\"token_uri\":\"https://accounts.google.com/o/oauth2/token\",\"auth_provider_x509_cert_url\":\"https://www.googleapis.com/oauth2/v1/certs\",\"client_secret\":\"fqHbRlaQ1Imh4bQNPdfths2U\",\"redirect_uris\":[\"urn:ietf:wg:oauth:2.0:oob\",\"http://localhost\"]}}";
    }
}
