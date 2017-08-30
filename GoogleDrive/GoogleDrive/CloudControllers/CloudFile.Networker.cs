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
            public static event RestRequests.ChunkSentEventHandler ChunkProcceeded, TotalAmountRemainChanged, TotalFilesRemainChanged, TotalFoldersRemainChanged;
            protected static void OnChunkProcceeded(long coda) { ChunkProcceeded?.Invoke(coda); }
            public static void OnTotalAmountRemainChanged(long coda) { TotalAmountRemainChanged?.Invoke(coda); }
            protected static void OnTotalFilesRemainChanged(long coda) { TotalFilesRemainChanged?.Invoke(coda); }
            protected static void OnTotalFoldersRemainChanged(long coda) { TotalFoldersRemainChanged?.Invoke(coda); }
            public delegate void NewTaskCreatedEventHandler(Networker networker);
            static Networker()
            {
                //MyLogger.Test2 = new Func<Task>(async () => { await MyLogger.Alert($"NetworkingCount: {NetworkingCount}"); });
                MyLogger.AddTestMethod("Release semaphoreSlim", new Func<Task>(async () =>
                   {
                       await Task.Delay(0);
                       ReleaseSemaphoreSlim();
                   }));
            }
            //public Networker()
            //{
            //    MessageAppended += (log) => { MyLogger.Log(log); };
            //}
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
            private volatile static SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(NetworkingMaxCount, NetworkingMaxCount);
            protected static async Task WaitSemaphoreSlimAsync()
            {
                await SemaphoreSlim.WaitAsync();
            }
            protected static void ReleaseSemaphoreSlim()
            {
                lock (SemaphoreSlim) SemaphoreSlim.Release();
            }
            //private volatile int WaitingProcessCount = 100;
            public enum NetworkStatus { NotStarted, ErrorNeedRestart, Networking, Paused, Completed };
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
            bool isPausing = false;
            public abstract Task ResetAsync();
            public async Task PauseAsync()
            {
                if(isPausing)
                {
                    OnMessageAppended("Pausing already in progress... Please wait...");
                    return;
                }
                isPausing = true;
                await PausePrivateAsync();
                isPausing = false;
            }
            public async Task WaitUntilCompletedAsync()
            {
                SemaphoreSlim semaphoreSlim = new SemaphoreSlim(0, 1);
                NetworkStatusChangedEventHandler statusChangedEventHandler = null;
                statusChangedEventHandler = new NetworkStatusChangedEventHandler(() =>
                {
                    if (Status == NetworkStatus.Completed)
                    {
                        StatusChanged -= statusChangedEventHandler;
                        lock(semaphoreSlim)semaphoreSlim.Release();
                    }
                });
                StatusChanged += statusChangedEventHandler;
                MyLogger.Assert(Status != NetworkStatus.Completed);
                index_Retry:;
                await semaphoreSlim.WaitAsync();
                if(Status != NetworkStatus.Completed)
                {
                    string msg = $"Status: {Status}, failed to WaitUntilCompletedAsync";
                    OnMessageAppended(msg);
                    MyLogger.Log(msg);
                    goto index_Retry;
                }
            }
            public async Task StartUntilCompletedAsync()
            {
                SemaphoreSlim semaphoreSlim = new SemaphoreSlim(0, 1);
                NetworkStatusChangedEventHandler statusChangedEventHandler = null;
                bool done = false;
                statusChangedEventHandler = new NetworkStatusChangedEventHandler(() =>
                  {
                      if (Status == NetworkStatus.Completed)
                      {
                          if (done) return;
                          done = true;
                          StatusChanged -= statusChangedEventHandler;
                          lock (semaphoreSlim)semaphoreSlim.Release();
                      }
                  });
                StatusChanged += statusChangedEventHandler;
                await StartAsync();
                index_Retry:;
                await semaphoreSlim.WaitAsync();
                if (Status != NetworkStatus.Completed)
                {
                    string msg = $"Status: {Status}, failed to StartUntilCompletedAsync";
                    OnMessageAppended(msg);
                    MyLogger.Log(msg);
                    goto index_Retry;
                }
            }
            public async Task StartAsync()
            {
                isPausing =false;
                await WaitSemaphoreSlimAsync();
                try
                {
                    if (isPausing)
                    {
                        //Status = NetworkStatus.Paused;
                        return;
                    }
                    await StartPrivateAsync();
                }
                catch (Exception error)
                {
                    throw error;
                }
                finally
                {
                    ReleaseSemaphoreSlim();
                }
            }
            protected abstract Task PausePrivateAsync();
            protected abstract Task StartPrivateAsync();
        }
    }
}
