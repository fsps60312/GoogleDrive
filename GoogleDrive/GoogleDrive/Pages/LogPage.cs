using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using GoogleDrive.MyControls;

namespace GoogleDrive
{
    class LogPage:MyContentPage
    {
        class MyTextBox:MyScrollView
        {
            StackLayout SLmain;
            public MyTextBox():base(ScrollOrientation.Both)
            {
                this.SizeChanged += async delegate { await this.ScrollToEnd(); };
                {
                    SLmain = new StackLayout { Orientation=StackOrientation.Vertical};
                    this.Content = SLmain;
                }
            }
            private async Task ScrollToEnd()
            {
                await this.ScrollToAsync(0, double.MaxValue, true);
                await this.ScrollToAsync(0, double.MaxValue, false);
            }
            int cnt = 0;
            public async Task AppendLine(string text)
            {
                if (SLmain.Children.Count > 1000) Clear();
                StackLayout sl = new StackLayout { Orientation = StackOrientation.Horizontal };
                sl.Children.Add(new Label { Text = $"#{++cnt}\t", LineBreakMode = LineBreakMode.NoWrap });
                sl.Children.Add(new Label { Text = $"{DateTime.Now}\t", LineBreakMode = LineBreakMode.NoWrap });
                sl.Children.Add(new Label { Text = text, LineBreakMode = LineBreakMode.NoWrap });
                SLmain.Children.Add(sl);
                await this.ScrollToEnd();
            }
            public void Clear()
            {
                this.Content = null;
                SLmain.Children.Clear();
                this.Content = SLmain;
            }
        }
        public LogPage():base("Log")
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
            //await CloudFile.AuthorizeAsync();
            //Old.Test2.Run();
            StatusCount = 0;
            MainStatus = "Done.";
            await Task.Delay(0);
        }
        private void RegisterEvents()
        {
            BTNclear.Clicked +=async delegate
              {
                  BTNclear.IsEnabled = false;
                  if (await MyLogger.Ask("Do you want to clear all the logs?"))
                  {
                      EDlog.Clear();
                      MyLogger.Log("All logs cleared.");
                  }
                  BTNclear.IsEnabled = true;
              };
            MyLogger.LogAppended += delegate (string log)
            {
                //log = $"#{++logCount}:\t{log}";
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
            //this.Width = 1600;
            //this.Height = 900;
            {
                GDmain = new MyGrid();
                GDmain.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                GDmain.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                GDmain.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                GDmain.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                GDmain.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20, GridUnitType.Absolute) });
                {
                    EDlog = new MyTextBox();
                    GDmain.Children.Add(new Frame { OutlineColor = Color.Accent, Padding = new Thickness(5), Content = EDlog }, 0, 0);
                }
                {
                    GDstatus = new Grid();
                    GDstatus.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    GDstatus.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Auto) });
                    {
                        LBstatus = new Label { Text = "Initializing..." };
                        GDstatus.Children.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = LBstatus }, 0, 0);
                    }
                    {
                        BTNclear = new Button { Text = "Clear" };
                        GDstatus.Children.Add(BTNclear, 1, 0);
                    }
                    GDmain.Children.Add(GDstatus, 0, 1);
                }
                {
                    GDstatus1 = new Grid();
                    GDstatus1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    GDstatus1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                    {
                        LBstatus1 = new Label { Text = "status 1", IsVisible = false };
                        GDstatus1.Children.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = LBstatus1 }, 0, 0);
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
                        GDstatus2.Children.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = LBstatus2 }, 0, 0);
                    }
                    {
                        PBstatus2 = new ProgressBar { Progress = 0.5, IsVisible = false };
                        Grid.SetColumn(PBstatus2, 1);
                        GDstatus2.Children.Add(PBstatus2);
                    }
                    Grid.SetRow(GDstatus2, 3);
                    GDmain.Children.Add(GDstatus2);
                }
                this.Content =new UnwipableContentView { Content = GDmain };
            }
        }
        MyTextBox EDlog;
        Grid GDmain,GDstatus, GDstatus1, GDstatus2;
        Button BTNclear;
        Label LBstatus, LBstatus1, LBstatus2;
        ProgressBar PBstatus1, PBstatus2;
    }
}
