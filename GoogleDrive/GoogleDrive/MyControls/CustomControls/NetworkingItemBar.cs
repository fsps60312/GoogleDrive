using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using Xamarin.Forms;

namespace GoogleDrive.MyControls
{
    class NetworkingItemBar : MyGrid, BarsListPanel.IDataBindedView<NetworkingItemBarViewModel>
    {
        public event BarsListPanel.DataBindedViewEventHandler<NetworkingItemBarViewModel> Appeared;
        public Func<Task> Disappearing { get; set; }
        public void Reset(NetworkingItemBarViewModel source)
        {
            if (this.BindingContext != null) (this.BindingContext as BarsListPanel.MyDisposable).UnregisterDisposingEvents();
            this.BindingContext = source;
            //BarsListPanel.MyDisposable.MyDisposableEventHandler eventHandler = new BarsListPanel.MyDisposable.MyDisposableEventHandler(
            if (source != null) source.Disposing = new Func<Task>(async () => { await Disappearing?.Invoke(); }); //MyDispossable will automatically unregister all Disposing events after disposed
            Appeared?.Invoke(this);
        }
        protected MyLabel LBname, LBspeed, LBstatus;
        protected MyProgressBar PBprogress;
        protected MyActivityIndicator AIprogress;
        protected MyButton BTNcontrol, BTNmessage;
        public NetworkingItemBar()
        {
            InitializeViews();
            System.Threading.SemaphoreSlim semaphoreSlim = new System.Threading.SemaphoreSlim(1, 1);
            this.Appeared +=async (sender) =>
            {
                this.Opacity = 0;
                await semaphoreSlim.WaitAsync();
                //this.Opacity = 1;
                await this.FadeTo(1, 500);
                lock (semaphoreSlim) semaphoreSlim.Release();
            };
            this.Disappearing = new Func<Task>( async() =>
            {
                await semaphoreSlim.WaitAsync();
                //this.Opacity = 0;
                await this.FadeTo(0, 500);
                lock (semaphoreSlim) semaphoreSlim.Release();
            });
        }
        public NetworkingItemBar(NetworkingItemBarViewModel source) : this()
        {
            this.Reset(source);
        }
        private void InitializeViews()
        {
            //this.GestureRecognizers.Add(new Xamarin.Forms.TapGestureRecognizer
            //{
            //    NumberOfTapsRequired = 1,
            //    Command = new Xamarin.Forms.Command(async () => { await MyLogger.Alert("Tapped"); })
            //});
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(0, Xamarin.Forms.GridUnitType.Absolute) });
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(4, Xamarin.Forms.GridUnitType.Star) });
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(4, Xamarin.Forms.GridUnitType.Star) });
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Star) });
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(50, Xamarin.Forms.GridUnitType.Absolute) });
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(50, Xamarin.Forms.GridUnitType.Absolute) });
            this.RowDefinitions.Add(new Xamarin.Forms.RowDefinition { Height = new Xamarin.Forms.GridLength(25, Xamarin.Forms.GridUnitType.Absolute) });
            this.RowDefinitions.Add(new Xamarin.Forms.RowDefinition { Height = new Xamarin.Forms.GridLength(5, Xamarin.Forms.GridUnitType.Absolute) });
            {
                LBname = new MyLabel("");
                LBname.SetBinding(MyLabel.TextProperty,new Xamarin.Forms.Binding("LBname"));
                this.Children.Add(LBname, 1, 0);
            }
            {
                AIprogress = new MyActivityIndicator();
                AIprogress.SetBinding(MyActivityIndicator.IsVisibleProperty, new Xamarin.Forms.Binding("AIvisible"));
                AIprogress.SetBinding(MyActivityIndicator.IsRunningProperty, new Xamarin.Forms.Binding("AIvisible"));
                this.Children.Add(AIprogress, 0, 1);
                MyGrid.SetColumnSpan(AIprogress, this.ColumnDefinitions.Count - 2);
            }
            {
                PBprogress = new MyProgressBar();
                PBprogress.SetBinding(MyProgressBar.IsVisibleProperty, new Xamarin.Forms.Binding("PBvisible"));
                PBprogress.SetBinding(MyProgressBar.ProgressProperty, new Xamarin.Forms.Binding("Progress"));
                this.Children.Add(PBprogress, 0, 1);
                MyGrid.SetColumnSpan(PBprogress, this.ColumnDefinitions.Count - 2);
            }
            {
                LBstatus = new MyLabel("");
                LBstatus.SetBinding(MyLabel.TextProperty, new Xamarin.Forms.Binding("LBstatus"));
                this.Children.Add(LBstatus, 2, 0);
            }
            {
                LBspeed = new MyLabel("1.404 MB/s");
                LBspeed.SetBinding(MyLabel.TextProperty, new Xamarin.Forms.Binding("LBspeed"));
                this.Children.Add(LBspeed, 3, 0);
            }
            {
                BTNmessage = new MyButton("");
                BTNmessage.SetBinding(MyButton.TextProperty, new Xamarin.Forms.Binding("BTNmessage"));
                BTNmessage.SetBinding(MyButton.IsEnabledProperty, new Xamarin.Forms.Binding("BTNmessageEnabled"));
                BTNmessage.SetBinding(MyButton.CommandProperty, new Xamarin.Forms.Binding("BTNmessageClicked"));
                this.Children.Add(BTNmessage, 4, 0);
                MyGrid.SetRowSpan(BTNmessage, 2);
            }
            {
                BTNcontrol = new MyButton("");
                BTNcontrol.SetBinding(MyButton.TextProperty, new Xamarin.Forms.Binding("BTNcontrol"));
                BTNcontrol.SetBinding(MyButton.IsEnabledProperty, new Xamarin.Forms.Binding("BTNcontrolEnabled"));
                BTNcontrol.SetBinding(MyButton.CommandProperty, new Xamarin.Forms.Binding("BTNcontrolClicked"));
                this.Children.Add(BTNcontrol, 5, 0);
                MyGrid.SetRowSpan(BTNcontrol, 2);
            }
        }
    }
    class NetworkingItemBarViewModel : BarsListPanel.MyDisposable, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected CloudFile.Networker networker;
        private string __LBname__;//= "(Initializing...)";
        private string __LBspeed__;// = "1.404 MB/s";
        private string __LBstatus__;// = "Downloading... (87.9487%) 1.23 MB / 7.6 GB";
        private double __Progress__ = -1;
        private string __BTNcontrol__ = "\u23f0";
        private bool __BTNcontrolEnabled__ = true;
        private System.Windows.Input.ICommand __BTNcontrolClicked__;
        private System.Windows.Input.ICommand __BTNmessageClicked__;
        public string BTNmessage { get { return "\u2139"; } }
        public System.Windows.Input.ICommand BTNcontrolClicked
        {
            private set
            {
                if (__BTNcontrolClicked__ == value) return;
                __BTNcontrolClicked__ = value;
                OnPropertyChanged("BTNcontrolClicked");
            }
            get { return __BTNcontrolClicked__; }
        }
        public System.Windows.Input.ICommand BTNmessageClicked
        {
            private set
            {
                if (__BTNmessageClicked__ == value) return;
                __BTNmessageClicked__ = value;
                OnPropertyChanged("BTNmessageClicked");
            }
            get { return __BTNmessageClicked__; }
        }
        public string LBname
        {
            private set
            {
                if (__LBname__ == value) return;
                __LBname__ = value;
                OnPropertyChanged("LBname");
            }
            get { return __LBname__; }
        }
        public string LBspeed
        {
            private set
            {
                if (__LBspeed__ == value) return;
                __LBspeed__ = value;
                OnPropertyChanged("LBspeed");
            }
            get { return __LBspeed__; }
        }
        public string LBstatus
        {
            private set
            {
                if (__LBstatus__ == value) return;
                __LBstatus__ = value;
                OnPropertyChanged("LBstatus");
            }
            get { return __LBstatus__; }
        }
        public double Progress
        {
            private set
            {
                if (__Progress__ == value) return;
                if((__Progress__==-1)!=(value==-1))
                {
                    OnPropertyChanged("PBvisible");
                    OnPropertyChanged("AIvisible");
                }
                __Progress__ = value;
                OnPropertyChanged("Progress");
            }
            get { return Math.Max(0.0, __Progress__); }
        }
        public bool PBvisible
        {
            get { return __Progress__ != -1; }
        }
        public bool AIvisible
        {
            get { return __Progress__ == -1; }
        }
        public string BTNcontrol
        {
            private set
            {
                if (__BTNcontrol__ == value) return;
                __BTNcontrol__ = value;
                OnPropertyChanged("BTNcontrol");
            }
            get { return __BTNcontrol__; }
        }
        public bool BTNcontrolEnabled
        {
            private set
            {
                if (__BTNcontrolEnabled__ == value) return;
                __BTNcontrolEnabled__ = value;
                OnPropertyChanged("BTNcontrolEnabled");
            }
            get { return __BTNcontrolEnabled__; }
        }
        public bool BTNmessageEnabled { private set; get; }
        private string messages = "";
        string[] status = new string[3];// { "Initializing", "(87.9487%) 1.23 MB / 7.6 GB","(log)" };
        //System.Threading.SemaphoreSlim semaphoreSlim = new System.Threading.SemaphoreSlim(1, 1);
        //DateTime lastUpdate = DateTime.MinValue;
        private void UpdateStatus(int i, string text)
        {
            status[i] = text;
            //var updateTime = DateTime.Now;
            //await semaphoreSlim.WaitAsync();
            //try
            //{
            //    if (updateTime <= lastUpdate) return;
            //    lastUpdate = updateTime;
            LBstatus = String.Join(" ", status);
            //    await Task.Delay(100);
            //}
            //finally
            //{
            //    lock (semaphoreSlim) semaphoreSlim.Release();
            //}
        }
        public NetworkingItemBarViewModel(CloudFile.Networker _networker)
        {
            networker = _networker;
            LBname = networker.ToString();
            RegisterNetworker();
            BTNcontrolClicked = new Xamarin.Forms.Command(async() =>
            {
                BTNcontrolEnabled = false;
                switch (networker.Status)
                {
                    case CloudFile.Networker.NetworkStatus.Networking:
                        {
                            await networker.PauseAsync();
                        }
                        break;
                    case CloudFile.Networker.NetworkStatus.ErrorNeedRestart:
                        {
                            await networker.ResetAsync();
                            await networker.StartAsync();
                        }
                        break;
                    case CloudFile.Networker.NetworkStatus.NotStarted:
                        {
                            await networker.StartAsync();
                        }
                        break;
                    case CloudFile.Networker.NetworkStatus.Paused:
                        {
                            await networker.StartAsync();
                        }
                        break;
                    case CloudFile.Networker.NetworkStatus.Completed:
                        {
                            await MyLogger.Alert("The task is already completed, no action to take");
                        }break;
                    default: throw new Exception($"networker.Status: {networker.Status}");
                }
            });
        }
        private void RegisterNetworker()
        {
            Networker_StatusChanged();
            networker.StatusChanged += Networker_StatusChanged;
            networker.ProgressChanged += Networker_ProgressChanged;
            networker.MessageAppended += Networker_MessageAppended;
        }
        private void Networker_MessageAppended(string msg)
        {
            if (messages == "")
            {
                BTNmessageEnabled = true;
                BTNmessageClicked = new Xamarin.Forms.Command(async () =>
               {
                   BTNmessageEnabled = false;
                   await MyLogger.Alert(String.Join("\r\n", networker.messages));
                   BTNmessageEnabled = true;
               });
            }
            messages += msg + "\r\n";
            UpdateStatus(2, msg);
            OnPropertyChanged("BTNmessageEnabled");
        }
        private void Networker_ProgressChanged(long now, long total)
        {
            double ratio = (now == 0 && total == 0 ? 0 : (double)now / total);
            Progress = ratio;
            UpdateStatus(1, $"({(ratio * 100).ToString("F3")}%) {now} / {total}");
        }
        private async void Networker_StatusChanged()
        {
            if (networker == null) return;//Believe it or not, this is really happening! Events will still be triggered even if you had -= it.
            string statusText = networker.Status.ToString();
            if (networker.Status == CloudFile.Networker.NetworkStatus.Networking)
            {
                if (networker.GetType() == typeof(CloudFile.Downloaders.FileDownloader)) statusText = "Downloading";
                if (networker.GetType() == typeof(CloudFile.Uploaders.FileUploader)) statusText = "Uploading";
                if (networker.GetType() == typeof(CloudFile.Uploaders.FolderUploader)) statusText = "Uploading Folder";
                if (networker.GetType() == typeof(CloudFile.Modifiers.FolderCreator)) statusText = "Creating Folder";
            }
            UpdateStatus(0, statusText);
            switch (networker.Status)
            {
                case CloudFile.Networker.NetworkStatus.Completed:
                    {
                        BTNcontrolEnabled = false;
                        BTNcontrol = "\u2714";
                        await System.Threading.Tasks.Task.Delay(1000);
                        await OnDisposed();
                    }
                    break;
                case CloudFile.Networker.NetworkStatus.Networking:
                    {
                        BTNcontrolEnabled = true;
                        BTNcontrol = "\u23f8";
                    }
                    break;
                case CloudFile.Networker.NetworkStatus.ErrorNeedRestart:
                    {
                        BTNcontrolEnabled = true;
                        BTNcontrol= "\u26a0";
                    }
                    break;
                case CloudFile.Networker.NetworkStatus.NotStarted:
                    {
                        BTNcontrolEnabled = true;
                        BTNcontrol = "\u23f0";
                    }
                    break;
                case CloudFile.Networker.NetworkStatus.Paused:
                    {
                        BTNcontrolEnabled = true;
                        BTNcontrol= "\u25b6";
                    }
                    break;
                //case CloudFile.Networker.NetworkStatus.Starting:
                //    {
                //        BTNcontrolEnabled = false;
                //    }
                //    break;
                default: throw new Exception($"networker.Status: {networker.Status}");
            }
        }
    }
}
