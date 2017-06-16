using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace GoogleDrive
{
    class LogPage:ContentPage
    {
        class MyTextBox:Editor//ScrollView
        {
            //Label LBmain;
            public MyTextBox()
            {
                //this.IsEnabled = false;
                //{
                //    LBmain = new Label() { BackgroundColor = Color.LightGoldenrodYellow };
                //    this.Content = LBmain;
                //}
            }
            //public string Text
            //{
            //    get { return LBmain.Text; }
            //}
            public async Task AppendLine(string text)
            {
                //LBmain.Text += $"{text}\r\n";
                //await this.ScrollToAsync(LBmain, ScrollToPosition.End, true);
                this.Text += $"{text}\r\n";
                if (this.Text.Length > 3000) this.Text = this.Text.Substring(this.Text.Length - 3000);
                this.Focus();
                await Task.Delay(0);
            }
        }
        public LogPage()
        {
            InitializeControls();
            RegisterEvents();
            DoAsyncInitializationTasks();
        }
        string MainStatus
        {
            get { return LBstatus.Text; }
            set
            {
                StatusCount = Math.Max(StatusCount, 1);
                LBstatus.Text = value;
            }
        }
        int StatusCount
        {
            get
            {
                if (!LBstatus.IsVisible) return 0;
                if (!GDstatus1.IsVisible) return 1;
                if (!GDstatus2.IsVisible) return 2;
                return 3;
            }
            set
            {
                MyLogger.Assert(0 <= value && value <= 3);
                LBstatus.IsVisible = value >= 1;
                GDstatus1.IsVisible = value >= 2;
                GDstatus2.IsVisible = value >= 3;
            }
        }
        private async void DoAsyncInitializationTasks()
        {
            await CloudFile.AuthorizeAsync();
            //Old.Test2.Run();
            StatusCount = 0;
            MainStatus = "Done.";
        }
        int logCount = 0;
        private void RegisterEvents()
        {
            MyLogger.LogAppended += delegate (string log)
            {
                log = $"#{++logCount}:\t{log}";
                Device.BeginInvokeOnMainThread(async() =>
                {
                    MainStatus = log;
                    await EDlog.AppendLine(log);
                });
            };
            MyLogger.Progress1Changed += delegate (double progress)
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    StatusCount = Math.Max(StatusCount, 2);
                    PBstatus1.IsVisible = true;
                    PBstatus1.Progress = progress;
                });
            };
            MyLogger.Progress2Changed += delegate (double progress)
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    StatusCount = Math.Max(StatusCount, 3);
                    PBstatus2.IsVisible = true;
                    PBstatus2.Progress = progress;
                });
            };
            MyLogger.Status1Changed += delegate (string status)
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    StatusCount = Math.Max(StatusCount, 2);
                    LBstatus1.IsVisible = true;
                    LBstatus1.Text = status;
                });
            };
            MyLogger.Status2Changed += delegate (string status)
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    StatusCount = Math.Max(StatusCount, 3);
                    LBstatus2.IsVisible = true;
                    LBstatus2.Text = status;
                });
            };
        }
        private void InitializeControls()
        {
            this.Title = "Log";
            //this.Width = 1600;
            //this.Height = 900;
            {
                GDmain = new Grid();
                GDmain.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                GDmain.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                GDmain.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                GDmain.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                {
                    EDlog = new MyTextBox();
                    GDmain.Children.Add(EDlog, 0, 0);
                }
                {
                    LBstatus = new Label { Text = "Initializing..." };
                    Grid.SetRow(LBstatus, 1);
                    GDmain.Children.Add(LBstatus);
                }
                {
                    GDstatus1 = new Grid();
                    GDstatus1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    GDstatus1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                    {
                        LBstatus1 = new Label { Text = "status 1", IsVisible = false };
                        Grid.SetColumn(LBstatus1, 0);
                        GDstatus1.Children.Add(LBstatus1);
                    }
                    {
                        PBstatus1 = new ProgressBar { Progress = 0.5, IsVisible = false };
                        Grid.SetColumn(PBstatus1, 1);
                        GDstatus1.Children.Add(PBstatus1);
                    }
                    Grid.SetRow(GDstatus1, 2);
                    GDmain.Children.Add(GDstatus1);
                }
                {
                    GDstatus2 = new Grid();
                    GDstatus2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    GDstatus2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                    {
                        LBstatus2 = new Label { Text = "status 2", IsVisible = false };
                        Grid.SetColumn(LBstatus2, 0);
                        GDstatus2.Children.Add(LBstatus2);
                    }
                    {
                        PBstatus2 = new ProgressBar { Progress = 0.5, IsVisible = false };
                        Grid.SetColumn(PBstatus2, 1);
                        GDstatus2.Children.Add(PBstatus2);
                    }
                    Grid.SetRow(GDstatus2, 3);
                    GDmain.Children.Add(GDstatus2);
                }
                this.Content = GDmain;
            }
        }
        MyTextBox EDlog;
        Grid GDmain, GDstatus1, GDstatus2;
        Label LBstatus, LBstatus1, LBstatus2;
        ProgressBar PBstatus1, PBstatus2;
    }
}
