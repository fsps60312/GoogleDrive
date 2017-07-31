using System;
using System.Collections.Generic;
using System.Text;

namespace GoogleDrive.MyControls
{
    class UploadItemBar:MyGrid
    {
        CloudFile.Uploaders.FileUploader uploader;
        MyLabel LBname, LBspeed, LBstatus;
        MyProgressBar PBprogress;
        MyActivityIndicator AIprogress;
        MyButton BTNcontrol, BTNmessage;
        string[] status = new string[] { "Initializing", "(87.9487%) 1.23 MB / 7.6 GB" };
        private void UpdateStatus(int i, string text)
        {
            status[i] = text;
            LBstatus.Text = String.Join(" ", status);
        }
        public UploadItemBar(CloudFile.Uploaders.FileUploader _uploader)
        {
            uploader = _uploader;
            InitializeViews();
            RegisterEvents();
            RegisterUploader();
        }
        ~UploadItemBar()
        {
            UnregisterUploader();
        }
        private void RegisterUploader()
        {
            uploader.StatusChanged += Uploader_StatusChanged;
            uploader.ProgressChanged += Uploader_ProgressChanged;
            uploader.MessageAppended += Uploader_MessageAppended;
        }
        string messages = "";
        private void Uploader_MessageAppended(string msg)
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

        private void UnregisterUploader()
        {
            uploader.StatusChanged -= Uploader_StatusChanged;
            uploader.ProgressChanged -= Uploader_ProgressChanged;
            uploader.MessageAppended -= Uploader_MessageAppended;
        }
        private void Uploader_ProgressChanged(object sender)
        {
            double ratio = (double)uploader.BytesUploaded / uploader.TotalFileLength;
            PBprogress.Progress = ratio;
            UpdateStatus(1, $"({(ratio * 100).ToString("F3")}%) {uploader.BytesUploaded} / {uploader.TotalFileLength}");
        }
        private void Uploader_StatusChanged(CloudFile.Uploaders.UploadStatus status)
        {
            switch (status)
            {
                case CloudFile.Uploaders.UploadStatus.Completed:
                    {
                        BTNcontrol.IsEnabled = false;
                        BTNcontrol.Text = "\u2714";
                    }
                    break;
                case CloudFile.Uploaders.UploadStatus.Uploading:
                    {
                        BTNcontrol.IsEnabled = true;
                        AIprogress.IsRunning = AIprogress.IsVisible = false;
                        PBprogress.IsVisible = true;
                        BTNcontrol.Text = "\u23f8";
                    }
                    break;
                case CloudFile.Uploaders.UploadStatus.ErrorNeedRestart:
                    {
                        BTNcontrol.IsEnabled = true;
                        BTNcontrol.Text = "\u26a0";
                    }
                    break;
                case CloudFile.Uploaders.UploadStatus.NotStarted:
                    {
                        BTNcontrol.IsEnabled = true;
                        BTNcontrol.Text = "\u25b6";
                    }
                    break;
                case CloudFile.Uploaders.UploadStatus.Paused:
                    {
                        BTNcontrol.IsEnabled = true;
                        BTNcontrol.Text = "\u25b6";
                    }
                    break;
                case CloudFile.Uploaders.UploadStatus.Starting:
                    {
                        BTNcontrol.IsEnabled = false;
                    }
                    break;
                default: throw new Exception($"status: {status}");
            }
            UpdateStatus(0, status.ToString());
        }
        private void RegisterEvents()
        {
            BTNcontrol.Clicked += BTNcontrol_Clicked;
        }

        private async void BTNcontrol_Clicked(object sender, EventArgs e)
        {
            BTNcontrol.IsEnabled = false;
            switch (uploader.Status)
            {
                case CloudFile.Uploaders.UploadStatus.Uploading:
                    {
                        await uploader.PauseAsync();
                    }
                    break;
                case CloudFile.Uploaders.UploadStatus.ErrorNeedRestart:
                    {
                        await uploader.ResetAsync();
                        await uploader.StartAsync();
                    }
                    break;
                case CloudFile.Uploaders.UploadStatus.NotStarted:
                    {
                        await uploader.StartAsync();
                    }
                    break;
                case CloudFile.Uploaders.UploadStatus.Paused:
                    {
                        await uploader.StartAsync();
                    }
                    break;
                default: throw new Exception($"downloader.Status: {uploader.Status}");
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
                LBstatus = new MyLabel("Uploading... (87.9487%) 1.23 MB / 7.6 GB");
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
