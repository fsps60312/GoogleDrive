using System;
using System.Collections.Generic;
using System.Text;
using GoogleDrive.MyControls;
using System.Threading.Tasks;
using System.Linq;
using Xamarin.Forms;

namespace GoogleDrive
{
    class FileTransferPage:MyContentPage
    {
        MyGrid GDmain,GDctrl;
        FileTransferContentView FTmain;
        volatile Dictionary<CloudFile.Networker.NetworkStatus, HashSet<CloudFile.Networker>> networkers = new Dictionary<CloudFile.Networker.NetworkStatus, HashSet<CloudFile.Networker>>();
        public FileTransferPage():base("File Transfer")
        {
            MyLogger.TestMethodAdded += (name, task) =>
              {
                  var ti = new MyToolbarItem(name);
                  ti.Clicked += async delegate { await task(); };
                  this.ToolbarItems.Add(ti);
              };
            {
                GDmain = new MyGrid();
                GDmain.RowDefinitions.Add(new Xamarin.Forms.RowDefinition { Height = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
                GDmain.RowDefinitions.Add(new Xamarin.Forms.RowDefinition { Height = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Star) });
                {
                    GDctrl = new MyGrid();
                    GDctrl.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
                    GDctrl.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Star) });
                    GDctrl.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
                    GDctrl.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
                    GDctrl.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
                    GDctrl.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
                    GDctrl.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
                    int columnNumber = 0;
                    {
                        var sw = new MySwitch("Auto Restart Failed tasks", "Manual Restart Failed Tasks", false);
                        FTmain = new FileTransferContentView();
                        sw.Toggled += delegate { FTmain.SetAutoRestartFailedTasks(sw.IsToggled); };
                        sw.IsToggled = true;
                        GDctrl.Children.Add(sw, columnNumber++, 0);
                    }
                    columnNumber++;
                    var actions = new Dictionary<CloudFile.Networker.NetworkStatus, Func<int,Task>>();
                    foreach(var status in Enum.GetValues(typeof(CloudFile.Networker.NetworkStatus)).Cast<CloudFile.Networker.NetworkStatus>())
                    {
                        networkers[status] = new HashSet<CloudFile.Networker>();
                        string text;
                        Func<CloudFile.Networker, Task> clickAction = null;
                        switch (status)
                        {
                            case CloudFile.Networker.NetworkStatus.Completed: text = "\u2714"; break;
                            case CloudFile.Networker.NetworkStatus.ErrorNeedRestart:
                                {
                                    text = "\u26a0";
                                    clickAction = new Func<CloudFile.Networker, Task>(async (networker) =>
                                      {
                                          await networker.ResetAsync();
                                          await networker.StartAsync();
                                      });
                                }break;
                            case CloudFile.Networker.NetworkStatus.Networking:
                                {
                                    text = "\u23f8";
                                    clickAction = new Func<CloudFile.Networker, Task>(async (networker) =>
                                      {
                                          await networker.PauseAsync();
                                      });
                                }
                                break;
                            case CloudFile.Networker.NetworkStatus.NotStarted:
                                {
                                    text = "\u23f0";
                                    clickAction = new Func<CloudFile.Networker, Task>(async (networker) =>
                                    {
                                        await networker.StartAsync();
                                    });
                                }
                                break;
                            case CloudFile.Networker.NetworkStatus.Paused:
                                {
                                    text = "\u25b6";
                                    clickAction = new Func<CloudFile.Networker, Task>(async (networker) =>
                                    {
                                        await networker.StartAsync();
                                    });
                                }
                                break;
                            default: throw new Exception($"status: {status}");
                        }
                        MyButton btn = new MyButton(text) { Opacity = 0 };
                        if (status == CloudFile.Networker.NetworkStatus.Completed) btn.IsEnabled = false;
                        btn.Clicked += async delegate
                          {
                              IEnumerable<Task> tasks;
                              lock (networkers)
                              {
                                  tasks = networkers[status].ToList().Select(async (networker) => { await clickAction(networker); });
                              }
                              await Task.WhenAll(tasks);
                          };
                        int number = 0;
                        System.Threading.SemaphoreSlim semaphoreSlim = new System.Threading.SemaphoreSlim(1, 1);
                        DateTime lastUpdate = DateTime.Now;
                        actions[status] = new Func<int, Task>(async (difference) =>
                          {
                              bool isZeroBefore = (number == 0);
                              number += difference;
                              DateTime updateTime = DateTime.Now;
                              await semaphoreSlim.WaitAsync();
                              try
                              {
                                  if (updateTime <= lastUpdate) return;
                                  lastUpdate = updateTime;
                                  btn.Text = $"{text}{number}";
                                  if (number == 0 && !isZeroBefore) await btn.FadeTo(0, 500);
                                  if (number != 0 && isZeroBefore) await btn.FadeTo(1, 500);
                                  await Task.Delay(100);
                              }
                              finally
                              {
                                  lock (semaphoreSlim) semaphoreSlim.Release();
                              }
                          });
                        GDctrl.Children.Add(btn, columnNumber++, 0);
                    }
                    FTmain.StatusEnter += async (networker, status) =>
                      {
                          networkers[status].Add(networker);
                          await actions[status](1);
                      };
                    FTmain.StatusLeave += async (networker, status) =>
                      {
                          networkers[status].Remove(networker);
                          await actions[status](-1);
                      };
                    GDmain.Children.Add(GDctrl, 0, 0);
                }
                {
                    GDmain.Children.Add(new Frame { OutlineColor = Color.Accent, Padding = new Thickness(5), Content = FTmain }, 0, 1);
                }
                this.Content = GDmain;
            }
        }
    }
    class FileTransferContentView : GoogleDrive.MyControls.BarsListPanel.BarsListPanel<NetworkingItemBar, NetworkingItemBarViewModel>//BasicStylePanel
    {
        public delegate void TaskStatusChangedEventHandler(CloudFile.Networker networker, CloudFile.Networker.NetworkStatus status);
        public event TaskStatusChangedEventHandler StatusLeave, StatusEnter;
        private void OnStatusEnter(CloudFile.Networker networker, CloudFile.Networker.NetworkStatus status) { StatusEnter?.Invoke(networker, status); }
        private void OnStatusLeave(CloudFile.Networker networker, CloudFile.Networker.NetworkStatus status) { StatusLeave?.Invoke(networker, status); }
        private volatile Dictionary<CloudFile.Networker, CloudFile.Networker.NetworkStatus> previousStatus = new Dictionary<CloudFile.Networker, CloudFile.Networker.NetworkStatus>();
        bool AutoRestartFailedTasks = false;
        public void SetAutoRestartFailedTasks(bool value)
        {
            if (value == AutoRestartFailedTasks) return;
            AutoRestartFailedTasks = value;
            todoWhenTaskFailed = (value == false ? null : new Func<CloudFile.Networker, Task>(async (networker) =>
                    {
                        await Task.Delay(500);
                        await networker.StartAsync();
                    }));
        }
        Func<CloudFile.Networker, Task> todoWhenTaskFailed = null;
        public FileTransferContentView()
        {
            System.Threading.SemaphoreSlim semaphoreSlim = new System.Threading.SemaphoreSlim(1, 1);
            var newTaskCreatedEventHandler = new CloudFile.Networker.NewTaskCreatedEventHandler((networker) =>
            {
                OnStatusEnter(networker, previousStatus[networker] = networker.Status);
                var statusChangedEventHandler = new CloudFile.Networker.NetworkStatusChangedEventHandler(async () =>
                  {
                      await semaphoreSlim.WaitAsync();
                      OnStatusLeave(networker, previousStatus[networker]);
                      OnStatusEnter(networker, previousStatus[networker] = networker.Status);
                      lock (semaphoreSlim) semaphoreSlim.Release();
                      if (networker.Status == CloudFile.Networker.NetworkStatus.ErrorNeedRestart)
                      {
                          if (todoWhenTaskFailed != null)
                          {
                              await todoWhenTaskFailed(networker);
                          }
                      }
                  });
                networker.StatusChanged += statusChangedEventHandler;
                var data = new NetworkingItemBarViewModel(networker);
                MyControls.BarsListPanel.MyDisposable.MyDisposableEventHandler dataDisposedEventHandler = null;
                dataDisposedEventHandler = new MyControls.BarsListPanel.MyDisposable.MyDisposableEventHandler(async delegate
                  {
                      networker.StatusChanged -= statusChangedEventHandler;
                      data.Disposed -= dataDisposedEventHandler;
                      await semaphoreSlim.WaitAsync();
                      OnStatusLeave(networker, previousStatus[networker]);
                      previousStatus.Remove(networker);
                      lock (semaphoreSlim) semaphoreSlim.Release();
                  });
                data.Disposed += dataDisposedEventHandler;
                this.PushBack(data);
            });
            CloudFile.Modifiers.FolderCreator.NewFolderCreateCreated += newTaskCreatedEventHandler;
            CloudFile.Downloaders.FileDownloader.NewFileDownloadCreated += newTaskCreatedEventHandler;
            CloudFile.Uploaders.FileUploader.NewFileUploadCreated += newTaskCreatedEventHandler;
            CloudFile.Uploaders.FolderUploader.NewFolderUploadCreated += newTaskCreatedEventHandler;
        }
    }
}
