using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Threading;

namespace GoogleDrive
{
    partial class CloudFile
    {
        public abstract class Networker
        {
            static Networker()
            {
                //MyLogger.Test2 = new Func<Task>(async () => { await MyLogger.Alert($"NetworkingCount: {NetworkingCount}"); });
                MyLogger.AddTestMethod("Release semaphoreSlim", new Func<Task>(async () =>
                   {
                       await Task.Delay(0);
                       semaphoreSlim.Release();
                   }));
            }
            //protected static volatile int __NetworkingCount__ = 0;
            //protected static int NetworkingCount
            //{
            //    get { return __NetworkingCount__; }
            //    set
            //    {
            //        __NetworkingCount__ = value;
            //        if (value < NetworkingMaxCount) semaphoreSlim.Release();
            //        //MyLogger.Log($"NetworkingCount: {value}");
            //    }
            //}
            protected const int NetworkingMaxCount = 20;
            private volatile static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(NetworkingMaxCount, NetworkingMaxCount);
            protected static async Task WaitSemaphoreSlim()
            {
                await semaphoreSlim.WaitAsync();
            }
            protected static void ReleaseSemaphoreSlim()
            {
                lock (semaphoreSlim)
                {
                    semaphoreSlim.Release();
                }
            }
            public bool IsBusy { get; protected set; } = false;
            //private volatile int WaitingProcessCount = 100;
            public enum NetworkStatus { NotStarted, Starting, ErrorNeedRestart, Networking, Paused, Completed };
            public delegate void NetworkStatusChangedEventHandler();
            public delegate void NetworkProgressChangedEventHandler(long now,long total);
            public event MessageAppendedEventHandler MessageAppended;
            public event NetworkStatusChangedEventHandler StatusChanged;
            public event NetworkProgressChangedEventHandler ProgressChanged;
            protected void OnMessageAppended(string msg) { messages.Add(msg); MessageAppended?.Invoke(msg); }
            protected void OnStatusChanged() { StatusChanged?.Invoke(); }
            protected void OnProgressChanged(long now,long total) { ProgressChanged?.Invoke(now, total); }
            NetworkStatus __Status__ = NetworkStatus.NotStarted;
            public NetworkStatus Status
            {
                get
                {
                    return __Status__;
                }
                protected set
                {
                    //if((__Status__==NetworkStatus.Networking)!=(value==NetworkStatus.Networking))
                    //{
                    //    if (value == NetworkStatus.Networking) NetworkingCount++;
                    //    else NetworkingCount--;
                    //}
                    __Status__ = value;
                    OnStatusChanged();
                }
            }
            public List<string> messages = new List<string>();
            public abstract Task ResetAsync();
            public abstract Task PauseAsync();
            public async Task StartAsync()
            {
                await semaphoreSlim.WaitAsync();
                try
                {
                    await StartPrivateAsync();
                }
                catch (Exception error)
                {
                    throw error;
                }
                finally
                {
                    semaphoreSlim.Release();
                }
            }
            protected abstract Task StartPrivateAsync();
        }
    }
}
