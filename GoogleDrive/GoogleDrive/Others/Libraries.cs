using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GoogleDrive
{
    class Libraries
    {
        static Libraries()
        {
            MyLogger.AddTestMethod("Generate Guid String", new Func<Task>(async () =>
             {
                 var guid = Guid.NewGuid();
                 string s = "";
                 foreach(var b in guid.ToByteArray())
                 {
                     s += (char)b;
                 }// s會變成亂碼
                 await MyLogger.Alert($"{guid.ToByteArray().Length}\r\n{guid}\r\n{s}\r\n{guid}");
                 // guid.ToString() 的形式： xxxxxxxx-xxxx-xxxx-xxxx-xxxx-xxxxxxxx (x是小寫英文or數字)
             }));
        }
        public static int[] GetFailArray(string s)
        {
            int[] fail = new int[s.Length + 1];
            fail[0] = fail[1] = 0;
            for (int i = 1; i < s.Length; i++)
            {
                int f = fail[i];
                while (f > 0 && s[f] != s[i]) f = fail[f];
                fail[i + 1] = (s[f] == s[i] ? f + 1 : f);
            }
            return fail;
        }
        public static string GetNonsubstring_A_Z(byte[]data)
        {
            string ans;
            while (new Func<string, bool>((str) =>
               {
                   var fail = GetFailArray(str);
                   for (int i = 0, u = 0; i < data.Length; i++)
                   {
                       while (u > 0 && data[i] != (byte)str[u]) u = fail[u];
                       if (data[i] == str[u])
                       {
                           ++u;
                           if (u == str.Length) return true;
                       }
                   }
                   return false;
               })(ans = Guid.NewGuid().ToString())) ;
            return ans;
        }
        public static string ByteArrayToString(byte[] ba)
        {
            System.Text.StringBuilder hex = new System.Text.StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
        public static async Task<string> GetSha256ForWindowsStorageFile(System.IO.Stream stream)
        {
            var streamLength = stream.Length;
            var md5 = System.Security.Cryptography.MD5.Create();
            md5.Initialize();
            var hash = await Task.Run(new Func<byte[]>(() => { return md5.ComputeHash(stream); }));
            if (stream.Length != streamLength) return null;
            return ByteArrayToString(hash);
        }
        class temporaryClassForGetSha256ForCloudFileById
        {
            public string md5Checksum;
        }
        public static async Task<string> GetSha256ForCloudFileById(string id)
        {
            string data = await (new RestRequests.FileGetter(id)).GetFileAsync();
            if (data == null) return null;
            data = data.Substring(data.IndexOf("{"));
            //MyLogger.Log(data);
            var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<temporaryClassForGetSha256ForCloudFileById>(data);
            //await MyLogger.Alert("hi");
            return obj.md5Checksum;
        }
    }
}
