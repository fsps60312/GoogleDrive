using System;
using System.Collections.Generic;
using System.Text;

namespace GoogleDrive.MyControls
{
    class NetworkingItemBar:MyGrid
    {
        //partial class ControlButton:MyButton
        //{
        //    public enum StatusEnum {Initializing, Start,Pause,Message,Error,Completed}
        //    StatusEnum __Status__;
        //    public StatusEnum Status
        //    {
        //        get { return __Status__; }
        //        set
        //        {
        //            __Status__ = value;
        //            string text = "";
        //            switch(__Status__)
        //            {
        //                case StatusEnum.Completed:text += "\u2714";break;
        //                case StatusEnum.Error:text += "\u26a0";break;
        //                case StatusEnum.Initializing:text += "\u23f0";break;
        //                case StatusEnum.Message:text += "\u2139";break;
        //                case StatusEnum.Pause:text += "\u23f8";break;
        //                case StatusEnum.Start:text += "\u25b6";break;
        //                default:throw new Exception($"__Status__: {__Status__}");
        //            }
        //            //text += __Status__.ToString();
        //            this.Text = text;
        //        }
        //    }
        //    int i = 0;
        //    public ControlButton():base("Hi\u2714\u2716\u2139\u26a0\u1f6a9\u25b6\u23f0\u23f8")
        //    {
        //        this.Clicked += ControlButton_Clicked;
        //        //this.WidthRequest = 150;
        //    }
        //    private void ControlButton_Clicked(object sender, EventArgs e)
        //    {
        //        Status = (StatusEnum)((i++)%6);
        //    }
        //}
        CloudFile.Networker networker;
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
        public NetworkingItemBar(CloudFile.Networker _networker)
        {
            networker = _networker;
            InitializeViews();
            RegisterEvents();
            RegisterNetworker();
        }
        ~NetworkingItemBar()
        {
            UnregisterNetworker();
        }
        private void RegisterNetworker()
        {
            networker.StatusChanged += Networker_StatusChanged;
            networker.ProgressChanged += Networker_ProgressChanged;
            networker.MessageAppended += Networker_MessageAppended;
        }
        private void UnregisterNetworker()
        {
            networker.StatusChanged -= Networker_StatusChanged;
            networker.ProgressChanged -= Networker_ProgressChanged;
            networker.MessageAppended -= Networker_MessageAppended;
        }
        string messages = "";
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
        private void RegisterEvents()
        {
            BTNcontrol.Clicked += BTNcontrol_Clicked;
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
