using System;
using System.Collections.Generic;
using System.Text;

namespace GoogleDrive.MyControls
{
    class CloudFileNetworkerMyDisposableVersion1: BarsListPanel.MyDisposable
    {
        public CloudFile.Networker networker
        {
            get;
            private set;
        }
        public CloudFileNetworkerMyDisposableVersion1(CloudFile.Networker _networker)
        {
            networker = _networker;
            networker.StatusChanged += Networker_StatusChanged;
        }
        private async void Networker_StatusChanged()
        {
            if (networker.Status == CloudFile.Networker.NetworkStatus.Completed)
            {
                await System.Threading.Tasks.Task.Delay(1000);
                OnDisposed();
            }
        }
        public bool aiVisible = true,controlButtonEnabled=false,msgButtonEnabled=false;
        public double progress = 0;
        public string LBname, LBspeed, LBstatus, messages;
    }
    class NetworkingItemBar1 : NetworkingItemBar, BarsListPanel.DataBindedView<CloudFileNetworkerMyDisposableVersion1>
    {
        //public static object Create()
        //{
        //    return new NetworkingItemBar() as NetworkingItemBar1;
        //}
        public void Reset(CloudFileNetworkerMyDisposableVersion1 c)
        {
            if(networker!=null)
            {
                
            }
            networker = c?.networker;
        }
    }
    class NetworkingItemBar:MyGrid
    {
        CloudFile.Networker __networker__=null;
        protected CloudFile.Networker networker
        {
            get { return __networker__; }
            set
            {
                if (__networker__ != null) UnregisterNetworker();
                __networker__ = value;
                if (__networker__ != null) RegisterNetworker();
            }
        }
        MyLabel LBname,LBspeed,LBstatus;
        MyProgressBar PBprogress;
        MyActivityIndicator AIprogress;
        MyButton BTNcontrol,BTNmessage;
        string[] status = new string[] { "Initializing", "(87.9487%) 1.23 MB / 7.6 GB" };
        private void UpdateStatus(int i,string text)
        {
            status[i] = text;
            LBstatus.Text = String.Join(" ", status);
        }
        public NetworkingItemBar()
        {
            InitializeViews();
        }
        public NetworkingItemBar(CloudFile.Networker _networker) : this()
        {
            networker = _networker;
        }
        ~NetworkingItemBar()
        {
            networker = null;
        }
        private void RegisterNetworker()
        {
            Networker_StatusChanged();
            BTNcontrol.Clicked += BTNcontrol_Clicked;
            networker.StatusChanged += Networker_StatusChanged;
            networker.ProgressChanged += Networker_ProgressChanged;
            networker.MessageAppended += Networker_MessageAppended;
        }
        //int cnt = 0;
        private void UnregisterNetworker()
        {
            BTNcontrol.Clicked -= BTNcontrol_Clicked;
            networker.StatusChanged -= Networker_StatusChanged;
            networker.ProgressChanged -= Networker_ProgressChanged;
            networker.MessageAppended -= Networker_MessageAppended;
        }
        protected string messages = "";
        private void Networker_MessageAppended(string msg)
        {
            if (!BTNmessage.IsEnabled)
            {
                BTNmessage.IsEnabled = true;
                BTNmessage.Clicked += async delegate
                {
                    BTNmessage.IsEnabled = false;
                    await MyLogger.Alert(messages);
                    BTNmessage.IsEnabled = true;
                };
            }
            messages += msg + "\r\n";
        }

        private void Networker_ProgressChanged(long now, long total)
        {
            double ratio = (double)now / total;
            PBprogress.Progress = ratio;
            UpdateStatus(1, $"({(ratio * 100).ToString("F3")}%) {now} / {total}");
        }

        private void Networker_StatusChanged()
        {
            if (networker == null) return;//Believe it or not, this is really happening! Events will still be triggered even if you had -= it.
            switch (networker.Status)
            {
                case CloudFile.Networker.NetworkStatus.Completed:
                    {
                        BTNcontrol.IsEnabled = false;
                        BTNcontrol.Text = "\u2714";
                    }
                    break;
                case CloudFile.Networker.NetworkStatus.Networking:
                    {
                        BTNcontrol.IsEnabled = true;
                        AIprogress.IsRunning = AIprogress.IsVisible = false;
                        PBprogress.IsVisible = true;
                        BTNcontrol.Text = "\u23f8";
                    }
                    break;
                case CloudFile.Networker.NetworkStatus.ErrorNeedRestart:
                    {
                        BTNcontrol.IsEnabled = true;
                        BTNcontrol.Text = "\u26a0";
                    }
                    break;
                case CloudFile.Networker.NetworkStatus.NotStarted:
                    {
                        BTNcontrol.IsEnabled = true;
                        BTNcontrol.Text = "\u25b6";
                    }
                    break;
                case CloudFile.Networker.NetworkStatus.Paused:
                    {
                        BTNcontrol.IsEnabled = true;
                        BTNcontrol.Text = "\u25b6";
                    }
                    break;
                case CloudFile.Networker.NetworkStatus.Starting:
                    {
                        BTNcontrol.IsEnabled = false;
                    }
                    break;
                default: throw new Exception($"networker.Status: {networker.Status}");
            }
            UpdateStatus(0, networker.Status.ToString());
        }
        private async void BTNcontrol_Clicked(object sender, EventArgs e)
        {
            BTNcontrol.IsEnabled = false;
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
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Star) });
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Star) });
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
            this.ColumnDefinitions.Add(new Xamarin.Forms.ColumnDefinition { Width = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
            this.RowDefinitions.Add(new Xamarin.Forms.RowDefinition { Height = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
            this.RowDefinitions.Add(new Xamarin.Forms.RowDefinition { Height = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
            {
                LBname = new MyLabel("(Initializing...)");
                this.Children.Add(LBname, 1, 0);
            }
            {
                AIprogress = new MyActivityIndicator
                {
                    IsRunning = true
                };
                this.Children.Add(AIprogress, 0, 1);
                MyGrid.SetColumnSpan(AIprogress, this.ColumnDefinitions.Count - 2);
            }
            {
                PBprogress = new MyProgressBar
                {
                    IsVisible = false
                };
                this.Children.Add(PBprogress, 0, 1);
                MyGrid.SetColumnSpan(PBprogress, this.ColumnDefinitions.Count - 2);
            }
            {
                LBstatus = new MyLabel("Downloading... (87.9487%) 1.23 MB / 7.6 GB");
                this.Children.Add(LBstatus, 2, 0);
            }
            {
                LBspeed = new MyLabel("1.404 MB/s");
                this.Children.Add(LBspeed, 3, 0);
            }
            {
                BTNmessage = new MyButton("\u2139");
                BTNmessage.IsEnabled = false;
                MyGrid.SetRowSpan(BTNmessage, 2);
                this.Children.Add(BTNmessage, 4, 0);
            }
            {
                BTNcontrol = new MyButton("\u23f0");
                MyGrid.SetRowSpan(BTNcontrol, 2);
                this.Children.Add(BTNcontrol, 5, 0);
            }
        }
    }
}
