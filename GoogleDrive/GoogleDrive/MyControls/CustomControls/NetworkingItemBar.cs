using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using Xamarin.Forms;

namespace GoogleDrive.MyControls
{
    class NetworkingItemBar : MyGrid, BarsListPanel.DataBindedView<NetworkingItemBarViewModel>
    {
        public event BarsListPanel.DataBindedViewEventHandler<NetworkingItemBarViewModel> Appeared;
        Func<Task> Disappearing = null;
        public void Reset(NetworkingItemBarViewModel source)
        {
            if (this.BindingContext != null) (this.BindingContext as NetworkingItemBarViewModel).UnregisterDisposingEvents();
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
            this.Appeared += (sender) =>
            {
                this.Animate("animation", new Animation(new Action<double>((ratio) => { this.Opacity = ratio; })), 16, 500);
            };
            this.Disappearing = new Func<Task>( async() =>
             {
                 var a = DateTime.Now;
                 this.Animate("animation", new Animation(new Action<double>((ratio) => { this.Opacity = 1.0 - ratio; })), 16, 500);
                 while (this.AnimationIsRunning("animation")) await Task.Delay(100);
                 MyLogger.Log((DateTime.Now - a).TotalMilliseconds.ToString());
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
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(2, Xamarin.Forms.GridUnitType.Star) });
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(2, Xamarin.Forms.GridUnitType.Star) });
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Star) });
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
            this.RowDefinitions.Add(new Xamarin.Forms.RowDefinition { Height = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
            this.RowDefinitions.Add(new Xamarin.Forms.RowDefinition { Height = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
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
                MyGrid.SetRowSpan(BTNmessage, 2);
                this.Children.Add(BTNmessage, 4, 0);
            }
            {
                BTNcontrol = new MyButton("");
                BTNcontrol.SetBinding(MyButton.TextProperty, new Xamarin.Forms.Binding("BTNcontrol"));
                BTNcontrol.SetBinding(MyButton.IsEnabledProperty, new Xamarin.Forms.Binding("BTNcontrolEnabled"));
                BTNcontrol.SetBinding(MyButton.CommandProperty, new Xamarin.Forms.Binding("BTNcontrolClicked"));
                MyGrid.SetRowSpan(BTNcontrol, 2);
                this.Children.Add(BTNcontrol, 5, 0);
            }
        }
    }
    //class NetworkingItemBar:MyGrid
    //{
    //    CloudFile.Networker __networker__=null;
    //    protected CloudFile.Networker networker
    //    {
    //        get { return __networker__; }
    //        set
    //        {
    //            if (__networker__ != null) UnregisterNetworker();
    //            __networker__ = value;
    //            if (__networker__ != null) RegisterNetworker();
    //        }
    //    }
    //    protected MyLabel LBname,LBspeed,LBstatus;
    //    protected MyProgressBar PBprogress;
    //    protected MyActivityIndicator AIprogress;
    //    protected MyButton BTNcontrol,BTNmessage;
    //    string[] status = new string[] { "Initializing", "(87.9487%) 1.23 MB / 7.6 GB" };
    //    private void UpdateStatus(int i,string text)
    //    {
    //        status[i] = text;
    //        LBstatus.Text = String.Join(" ", status);
    //    }
    //    public NetworkingItemBar()
    //    {
    //        InitializeViews();
    //    }
    //    public NetworkingItemBar(CloudFile.Networker _networker) : this()
    //    {
    //        networker = _networker;
    //    }
    //    ~NetworkingItemBar()
    //    {
    //        networker = null;
    //    }
    //    private void RegisterNetworker()
    //    {
    //        Networker_StatusChanged();
    //        BTNcontrol.Clicked += BTNcontrol_Clicked;
    //        networker.StatusChanged += Networker_StatusChanged;
    //        networker.ProgressChanged += Networker_ProgressChanged;
    //        networker.MessageAppended += Networker_MessageAppended;
    //    }
    //    //int cnt = 0;
    //    private void UnregisterNetworker()
    //    {
    //        BTNcontrol.Clicked -= BTNcontrol_Clicked;
    //        networker.StatusChanged -= Networker_StatusChanged;
    //        networker.ProgressChanged -= Networker_ProgressChanged;
    //        networker.MessageAppended -= Networker_MessageAppended;
    //    }
    //    private void Networker_MessageAppended(string msg)
    //    {
    //        if (!BTNmessage.IsEnabled)
    //        {
    //            BTNmessage.IsEnabled = true;
    //            BTNmessage.Clicked += async delegate
    //            {
    //                BTNmessage.IsEnabled = false;
    //                await MyLogger.Alert(String.Join("\r\n", networker.messages));
    //                BTNmessage.IsEnabled = true;
    //            };
    //        }
    //    }
    //    private void Networker_ProgressChanged(long now, long total)
    //    {
    //        double ratio = (double)now / total;
    //        PBprogress.Progress = ratio;
    //        UpdateStatus(1, $"({(ratio * 100).ToString("F3")}%) {now} / {total}");
    //    }
    //    private void Networker_StatusChanged()
    //    {
    //        if (networker == null) return;//Believe it or not, this is really happening! Events will still be triggered even if you had -= it.
    //        switch (networker.Status)
    //        {
    //            case CloudFile.Networker.NetworkStatus.Completed:
    //                {
    //                    BTNcontrol.IsEnabled = false;
    //                    BTNcontrol.Text = "\u2714";
    //                }
    //                break;
    //            case CloudFile.Networker.NetworkStatus.Networking:
    //                {
    //                    BTNcontrol.IsEnabled = true;
    //                    AIprogress.IsRunning = AIprogress.IsVisible = false;
    //                    PBprogress.IsVisible = true;
    //                    BTNcontrol.Text = "\u23f8";
    //                }
    //                break;
    //            case CloudFile.Networker.NetworkStatus.ErrorNeedRestart:
    //                {
    //                    BTNcontrol.IsEnabled = true;
    //                    BTNcontrol.Text = "\u26a0";
    //                }
    //                break;
    //            case CloudFile.Networker.NetworkStatus.NotStarted:
    //                {
    //                    BTNcontrol.IsEnabled = true;
    //                    BTNcontrol.Text = "\u25b6";
    //                }
    //                break;
    //            case CloudFile.Networker.NetworkStatus.Paused:
    //                {
    //                    BTNcontrol.IsEnabled = true;
    //                    BTNcontrol.Text = "\u25b6";
    //                }
    //                break;
    //            case CloudFile.Networker.NetworkStatus.Starting:
    //                {
    //                    BTNcontrol.IsEnabled = false;
    //                }
    //                break;
    //            default: throw new Exception($"networker.Status: {networker.Status}");
    //        }
    //        string statusText = networker.Status.ToString();
    //        if (networker.GetType() == typeof(CloudFile.Downloaders.FileDownloader)) statusText = "Downloading";
    //        if (networker.GetType() == typeof(CloudFile.Uploaders.FileUploader)) statusText = "Uploading";
    //        if (networker.GetType() == typeof(CloudFile.Modifiers.FolderCreater)) statusText = "Creating Folder";
    //        UpdateStatus(0,statusText);
    //    }
    //    private async void BTNcontrol_Clicked(object sender, EventArgs e)
    //    {
    //        BTNcontrol.IsEnabled = false;
    //        switch (networker.Status)
    //        {
    //            case CloudFile.Networker.NetworkStatus.Networking:
    //                {
    //                    await networker.PauseAsync();
    //                }
    //                break;
    //            case CloudFile.Networker.NetworkStatus.ErrorNeedRestart:
    //                {
    //                    await networker.ResetAsync();
    //                    await networker.StartAsync();
    //                }
    //                break;
    //            case CloudFile.Networker.NetworkStatus.NotStarted:
    //                {
    //                    await networker.StartAsync();
    //                }
    //                break;
    //            case CloudFile.Networker.NetworkStatus.Paused:
    //                {
    //                    await networker.StartAsync();
    //                }
    //                break;
    //            default: throw new Exception($"networker.Status: {networker.Status}");
    //        }
    //    }
    //    private void InitializeViews()
    //    {
    //        //this.GestureRecognizers.Add(new Xamarin.Forms.TapGestureRecognizer
    //        //{
    //        //    NumberOfTapsRequired = 1,
    //        //    Command = new Xamarin.Forms.Command(async () => { await MyLogger.Alert("Tapped"); })
    //        //});
    //        this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
    //        this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(2, Xamarin.Forms.GridUnitType.Star) });
    //        this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Star) });
    //        this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Star) });
    //        this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
    //        this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
    //        this.RowDefinitions.Add(new Xamarin.Forms.RowDefinition { Height = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
    //        this.RowDefinitions.Add(new Xamarin.Forms.RowDefinition { Height = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
    //        {
    //            LBname = new MyLabel("(Initializing...)");
    //            this.Children.Add(LBname, 1, 0);
    //        }
    //        {
    //            AIprogress = new MyActivityIndicator
    //            {
    //                IsRunning = true
    //            };
    //            this.Children.Add(AIprogress, 0, 1);
    //            MyGrid.SetColumnSpan(AIprogress, this.ColumnDefinitions.Count - 2);
    //        }
    //        {
    //            PBprogress = new MyProgressBar
    //            {
    //                IsVisible = false
    //            };
    //            this.Children.Add(PBprogress, 0, 1);
    //            MyGrid.SetColumnSpan(PBprogress, this.ColumnDefinitions.Count - 2);
    //        }
    //        {
    //            LBstatus = new MyLabel("Downloading... (87.9487%) 1.23 MB / 7.6 GB");
    //            this.Children.Add(LBstatus, 2, 0);
    //        }
    //        {
    //            LBspeed = new MyLabel("1.404 MB/s");
    //            this.Children.Add(LBspeed, 3, 0);
    //        }
    //        {
    //            BTNmessage = new MyButton("\u2139");
    //            BTNmessage.IsEnabled = false;
    //            MyGrid.SetRowSpan(BTNmessage, 2);
    //            this.Children.Add(BTNmessage, 4, 0);
    //        }
    //        {
    //            BTNcontrol = new MyButton("\u23f0");
    //            MyGrid.SetRowSpan(BTNcontrol, 2);
    //            this.Children.Add(BTNcontrol, 5, 0);
    //        }
    //    }
    //}
    class NetworkingItemBarViewModel : BarsListPanel.MyDisposable, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected CloudFile.Networker networker;
        private string __LBname__= "(Initializing...)";
        private string __LBspeed__ = "1.404 MB/s";
        private string __LBstatus__ = "Downloading... (87.9487%) 1.23 MB / 7.6 GB";
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
        string[] status = new string[] { "Initializing", "(87.9487%) 1.23 MB / 7.6 GB","(log)" };
        private void UpdateStatus(int i, string text)
        {
            status[i] = text;
            LBstatus = String.Join(" ", status);
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
            double ratio = (double)now / total;
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
                if (networker.GetType() == typeof(CloudFile.Modifiers.FolderCreater)) statusText = "Creating Folder";
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
                        Progress = 0;
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
                        BTNcontrol = "\u25b6";
                    }
                    break;
                case CloudFile.Networker.NetworkStatus.Paused:
                    {
                        BTNcontrolEnabled = true;
                        BTNcontrol= "\u25b6";
                    }
                    break;
                case CloudFile.Networker.NetworkStatus.Starting:
                    {
                        BTNcontrolEnabled = false;
                    }
                    break;
                default: throw new Exception($"networker.Status: {networker.Status}");
            }
        }
    }
}
