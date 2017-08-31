using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GoogleDrive
{
    class Libraries
    {
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
            //await MyLogger.Alert($"a {stream.Length}");
            //long i = 0;
            var md5 = System.Security.Cryptography.MD5.Create();
            md5.Initialize();
            var hash = await Task.Run(new Func<byte[]>(() => { return md5.ComputeHash(stream); }));
            //while (i< stream.Length)//Has Oplock issues
            //{
            //    var l = await stream.ReadAsync(buffer, 0, bufferLen);
            //    i += l;
            //}
            //await MyLogger.Alert("b");
            if (stream.Length != streamLength) return null;
            return ByteArrayToString(hash);
            ////NB: "file" is a "StorageFile" previously opened
            ////in this example I use HashAlgorithmNames.Md5, you can replace it with HashAlgorithmName.Sha1, etc...

            //var alg = Windows.Security.Cryptography.Core.HashAlgorithmProvider.OpenAlgorithm(Windows.Security.Cryptography.Core.HashAlgorithmNames.Md5);
            //var stream = await windowsFile.OpenStreamForReadAsync();
            //var inputStream = stream.AsInputStream();
            //uint capacity = 100000000;
            //Windows.Storage.Streams.Buffer buffer = new Windows.Storage.Streams.Buffer(capacity);
            //var hash = alg.CreateHash();

            //while (true)
            //{
            //    await inputStream.ReadAsync(buffer, capacity,Windows.Storage.Streams.InputStreamOptions.None);

            //    if (buffer.Length > 0)
            //        hash.Append(buffer);
            //    else
            //        break;
            //}

            //string hashText =Windows.Security.Cryptography.CryptographicBuffer.EncodeToHexString(hash.GetValueAndReset()).ToUpper();

            //inputStream.Dispose();
            //stream.Dispose();
            //return hashText;
        }
        class temporaryClassForGetSha256ForCloudFileById
        {
            public string md5Checksum;
        }
        public static async Task<string> GetSha256ForCloudFileById(string id)
        {
            var data = await (new RestRequests.FileGetter(id)).GetFileAsync();
            data = data.Substring(data.IndexOf("{"));
            //MyLogger.Log(data);
            var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<temporaryClassForGetSha256ForCloudFileById>(data);
            //await MyLogger.Alert("hi");
            return obj.md5Checksum;
        }
    }
}
