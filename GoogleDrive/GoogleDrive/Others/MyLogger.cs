﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Xamarin.Forms;

namespace GoogleDrive
{
    static class MyLogger
    {
        public delegate void TestMethodAddedEventHandler(string name, Func<Task> task);
        public static event TestMethodAddedEventHandler TestMethodAdded;
        private volatile static List<KeyValuePair<string, Func<Task>>> TestMethodNotAdded = new List<KeyValuePair<string, Func<Task>>>();
        static volatile int TestMethodAddedCnt = 0;
        public static void AddTestMethod(string name, Func<Task> task)
        {
            Log($"cnt: {++TestMethodAddedCnt}, {name}");
            if (TestMethodAdded == null)
            {
                TestMethodNotAdded.Add(new KeyValuePair<string, Func<Task>>(name, task));
            }
            else
            {
                foreach (var p in TestMethodNotAdded)
                {
                    TestMethodAdded.Invoke(p.Key, p.Value);
                    Log(p.Key);
                }
                TestMethodNotAdded.Clear();
                TestMethodAdded.Invoke(name, task);
                Log(name);
            }
        }
        public delegate void LogAppendedEventHandler(string log);
        public static event LogAppendedEventHandler LogAppended;
        public static void Log(string log) { System.Diagnostics.Debug.WriteLine(log); Status = log; LogAppended?.Invoke(log); }
        public static string Status { get; private set; }
        public static async Task Alert(string msg) { await App.Current.MainPage.DisplayAlert("", msg, "OK"); }
        public static async Task<bool> Ask(string msg) { return await App.Current.MainPage.DisplayAlert("", msg, "OK", "Cancel"); }
        public static void Assert(bool condition) { if (!condition) MyLogger.Log("Assertion failed!"); System.Diagnostics.Debug.Assert(condition); }
        private static async Task WriteFileInWindowsAsync(Windows.Storage.StorageFolder folder, string fileName, string content)
        {
            int index = fileName.IndexOf('/');
            if (index == -1)
            {
                var file = await folder.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.ReplaceExisting);
                var stream = await file.OpenStreamForWriteAsync();
                using (var writer = new StreamWriter(stream))
                {
                    await writer.WriteAsync(content);
                }
                return;
            }
            else
            {
                await WriteFileInWindowsAsync(await folder.CreateFolderAsync(fileName.Remove(index), Windows.Storage.CreationCollisionOption.OpenIfExists), fileName.Substring(index + 1), content);
            }
        }
        private static async Task<string> ReadFileInWindowsAsync(Windows.Storage.StorageFolder folder, string fileName)
        {
            int index = fileName.IndexOf('/');
            if (index == -1)
            {
                var file = await folder.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.OpenIfExists);
                using (var stream = await file.OpenStreamForReadAsync())
                {
                    using (var reader = new StreamReader(stream))
                    {
                        return await reader.ReadToEndAsync();
                    }
                }
            }
            else
            {
                return await ReadFileInWindowsAsync(await folder.CreateFolderAsync(fileName.Remove(index), Windows.Storage.CreationCollisionOption.OpenIfExists), fileName.Substring(index + 1));
            }
        }
        public static async Task WriteFileAsync(string fileName, string content)
        {
            switch (Device.RuntimePlatform)
            {
                case Device.Windows:
                    await WriteFileInWindowsAsync(Windows.Storage.ApplicationData.Current.LocalFolder, fileName, content);
                    break;
                default:
                    await Alert($"Writing files not supported on {Device.RuntimePlatform} platform");
                    break;
            }
        }
        public static async Task<string> ReadFileAsync(string fileName)
        {
            switch (Device.RuntimePlatform)
            {
                case Device.Windows:
                    return await ReadFileInWindowsAsync(Windows.Storage.ApplicationData.Current.LocalFolder, fileName);
                default:
                    await Alert($"Reading files not supported on {Device.RuntimePlatform} platform");
                    return null;
            }
        }
    }
}