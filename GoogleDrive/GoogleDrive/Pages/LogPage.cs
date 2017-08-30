using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using GoogleDrive.MyControls;
using GoogleDrive.MyControls.BarsListPanel;
using System.ComponentModel;
using System.Windows.Input;

namespace GoogleDrive
{
    class LogPage:MyContentPage
    {
        static string MakeOneLine(string s)
        {
            return s.Replace("\r\n", "\t").Replace('\n', '\t').Replace('\r', '\t');
            //int i = s.IndexOf("\n");
            //if (i == -1) return s;
            //else if (i > 0 && s[i - 1] == '\r') return s.Remove(i - 1) + "... (Tap to Expand)";
            //else return s.Remove(i) + "... (Tap to Expand)";
        }
        class LogPageItemBarViewModel : MyDisposable, INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            private string __Number__;
            private string __Time__;
            private string __Log__;
            private ICommand __Tapped__;
            private FontAttributes __FontAttributes__;
            public string Number
            {
                private set
                {
                    __Number__ = value;
                    OnPropertyChanged("Number");
                }
                get { return __Number__; }
            }
            public string Time
            {
                private set
                {
                    __Time__ = value;
                    OnPropertyChanged("Time");
                }
                get { return __Time__; }
            }
            public string Log
            {
                private set
                {
                    __Log__ = value;
                    OnPropertyChanged("Log");
                }
                get { return __Log__; }
            }
            public ICommand Tapped
            {
                private set
                {
                    __Tapped__ = value;
                    OnPropertyChanged("Tapped");
                }
                get { return __Tapped__; }
            }
            public FontAttributes FontAttributes
            {
                private set
                {
                    __FontAttributes__ = value;
                    OnPropertyChanged("FontAttributes");
                }
                get { return __FontAttributes__; }
            }
            static volatile int counter = 0;
            DateTime time = DateTime.Now;
            int number = ++counter;
            string log;
            static int LineCount(string s)
            {
                int ans = 1, i = -1;
                while ((i = s.IndexOf("\n", i + 1)) != -1) ++ans;
                return ans;
            }
            bool isExpanded = false;
            int lineCount;
            public LogPageItemBarViewModel(string _log)
            {
                log = _log;
                Number = $"#{number}  \t";
                Time = $"{time}  \t";
                //Log = MakeOneLine(log);// +"#"+LineCount(log).ToString();
                lineCount = LineCount(log);
                if (lineCount == 1) Log = log;
                else Log = $"[Tap to Expand]{MakeOneLine(log)}";
                //FontAttributes = (lineCount > 1 ? FontAttributes.Bold : FontAttributes.None);
                Tapped = new Command(() =>
                  {
                      if (lineCount == 1) return;
                      isExpanded ^= true;
                      if (isExpanded)
                      {
                          //FontAttributes=FontAttributes.Italic;
                          Log = log;
                          OnHeightChanged((lineCount - 1) * 19.5);
                      }
                      else
                      {
                          //FontAttributes = FontAttributes.Bold;
                          Log = $"[Tap to Expand]{MakeOneLine(log)}";
                          OnHeightChanged(-(lineCount - 1) * 19.5);
                      }
                  });
            }
        }
        class LogPageItemBar : MyGrid, IDataBindedView<LogPageItemBarViewModel>
        {
            public event DataBindedViewEventHandler<LogPageItemBarViewModel> Appeared;
            public Func<Task> Disappearing { get; set; }
            public void Reset(LogPageItemBarViewModel source)
            {
                if (this.BindingContext != null) (this.BindingContext as MyDisposable).UnregisterDisposingEvents();
                this.BindingContext = source;
                //BarsListPanel.MyDisposable.MyDisposableEventHandler eventHandler = new BarsListPanel.MyDisposable.MyDisposableEventHandler(
                if (source != null) source.Disposing = new Func<Task>(async () => { await Disappearing?.Invoke(); }); //MyDispossable will automatically unregister all Disposing events after disposed
                Appeared?.Invoke(this);
            }
            MyLabel LBnumber, LBtime, LBlog;
            private void InitializeViews()
            {
                this.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                this.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                this.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                {
                    LBnumber = new MyLabel("Number");
                    LBnumber.SetBinding(MyLabel.TextProperty, new Binding("Number"));
                    LBnumber.LineBreakMode = LineBreakMode.NoWrap;
                    this.Children.Add(LBnumber, 0, 0);
                }
                {
                    LBtime = new MyLabel("Time");
                    LBtime.SetBinding(MyLabel.TextProperty, new Binding("Time"));
                    LBtime.LineBreakMode = LineBreakMode.NoWrap;
                    this.Children.Add(LBtime, 1, 0);
                }
                {
                    LBlog = new MyLabel("Log");
                    LBlog.SetBinding(MyLabel.TextProperty, new Binding("Log"));
                    LBlog.SetBinding(MyLabel.FontAttributesProperty, new Binding("FontAttributes"));
                    LBlog.LineBreakMode = LineBreakMode.NoWrap;
                    //LBlog.SetBinding(MyLabel.LineBreakModeProperty, new Binding("LineBreakMode"));
                    this.Children.Add(LBlog, 2, 0);
                }
                var gestureRecognizer = new TapGestureRecognizer();
                gestureRecognizer.SetBinding(TapGestureRecognizer.CommandProperty, "Tapped");
                this.GestureRecognizers.Add(gestureRecognizer);
                //StackLayout sl = new StackLayout { Orientation = StackOrientation.Horizontal };
                //sl.Children.Add(new Label { Text = $"#{++cnt}\t", LineBreakMode = LineBreakMode.NoWrap });
                //sl.Children.Add(new Label { Text = $"{DateTime.Now}\t", LineBreakMode = LineBreakMode.NoWrap });
                //sl.Children.Add(new Label { Text = text, LineBreakMode = LineBreakMode.NoWrap });
            }
            public LogPageItemBar()
            {
                InitializeViews();
                this.Appeared += async (sender) =>
                {
                    this.Opacity = 0;
                    await this.FadeTo(1, 500);
                };
                this.Disappearing = new Func<Task>(async () =>
                {
                    await this.FadeTo(0, 500);
                });
            }
            public LogPageItemBar(LogPageItemBarViewModel source) : this()
            {
                this.Reset(source);
            }
        }
        class LogPanel : BarsListPanel<LogPageItemBar, LogPageItemBarViewModel>
        {
            public bool autoScroll = true;
            public LogPanel()
            {
                this.BarsLayoutMethod = new Func<double, Tuple<Rectangle, AbsoluteLayoutFlags>>((y) =>
                  {
                      return new Tuple<Rectangle, AbsoluteLayoutFlags>(new Rectangle(0, y, -1, -1), AbsoluteLayoutFlags.None);
                  });
                this.SVmain.Orientation = ScrollOrientation.Both;
                this.ItemHeight = 25;
                MyLogger.LogAppended += async(log) =>
                {
                    this.PushBack(new LogPageItemBarViewModel(log));
                    if (autoScroll)
                    {
                        await this.ScrollToEnd();
                        await Task.Delay((int)this.AnimationDuration);
                        await this.ScrollToEnd();
                    }
                };
            }
        }
        //class MyTextBox:MyScrollView
        //{
        //    StackLayout SLmain;
        //    public MyTextBox():base(ScrollOrientation.Both)
        //    {
        //        this.SizeChanged += async delegate { await this.ScrollToEnd(); };
        //        {
        //            SLmain = new StackLayout { Orientation=StackOrientation.Vertical};
        //            this.Content = SLmain;
        //        }
        //    }
        //    private async Task ScrollToEnd()
        //    {
        //        await this.ScrollToAsync(0, double.MaxValue, true);
        //        await this.ScrollToAsync(0, double.MaxValue, false);
        //    }
        //    int cnt = 0;
        //    public async Task AppendLine(string text)
        //    {
        //        if (SLmain.Children.Count > 1000) Clear();
        //        StackLayout sl = new StackLayout { Orientation = StackOrientation.Horizontal };
        //        sl.Children.Add(new Label { Text = $"#{++cnt}\t", LineBreakMode = LineBreakMode.NoWrap });
        //        sl.Children.Add(new Label { Text = $"{DateTime.Now}\t", LineBreakMode = LineBreakMode.NoWrap });
        //        sl.Children.Add(new Label { Text = text, LineBreakMode = LineBreakMode.NoWrap });
        //        SLmain.Children.Add(sl);
        //        await this.ScrollToEnd();
        //    }
        //    public void Clear()
        //    {
        //        this.Content = null;
        //        SLmain.Children.Clear();
        //        this.Content = SLmain;
        //    }
        //}
        public LogPage():base("Log")
        {
            InitializeControls();
            RegisterEvents();
        }
        private void RegisterEvents()
        {
            MyLogger.LogAppended += (log) => { LBstatus.Text = MakeOneLine(log); };
            SWautoScroll.Toggled += delegate
              {
                  EDlog.autoScroll = SWautoScroll.IsToggled;
              };
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
                    //EDlog = new MyTextBox();
                    EDlog = new LogPanel();
                    GDmain.Children.Add(new Frame { OutlineColor = Color.Accent, Padding = new Thickness(5), Content = EDlog }, 0, 0);
                }
                {
                    GDstatus = new Grid();
                    GDstatus.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    GDstatus.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                    GDstatus.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                    //GDstatus.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                    int columnNumber = 0;
                    {
                        LBstatus = new Label { Text = "Initializing..." };
                        GDstatus.Children.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = LBstatus }, columnNumber++, 0);
                    }
                    //{
                    //    var lbl = new MyLabel("Auto Scroll") { IsVisible = false, Opacity = 0, FontAttributes = FontAttributes.Bold, VerticalTextAlignment = TextAlignment.Center };
                    //    SWautoScroll = new MySwitch("Auto Scroll", "Manual Scroll") { IsToggled = true };
                    //    System.Threading.SemaphoreSlim semaphoreSlim = new System.Threading.SemaphoreSlim(1, 1);
                    //    bool animationCompletedWith = true;
                    //    SWautoScroll.Toggled += async delegate
                    //    {
                    //        bool backUp = SWautoScroll.IsToggled;
                    //        try
                    //        {
                    //            await semaphoreSlim.WaitAsync();
                    //            lbl.Text = (SWautoScroll.IsToggled ? "Auto Scroll" : "Manual Scroll");
                    //            if (backUp != SWautoScroll.IsToggled || animationCompletedWith == backUp) return;
                    //            lbl.IsVisible = true;
                    //            await lbl.FadeTo(1);
                    //            await Task.Delay(1000);
                    //            await lbl.FadeTo(0);
                    //            lbl.IsVisible = false;
                    //            animationCompletedWith = backUp;
                    //        }
                    //        finally
                    //        {
                    //            lock (semaphoreSlim)semaphoreSlim.Release();
                    //        }
                    //    };
                    //    GDstatus.Children.Add(lbl, columnNumber++, 0);
                    //}
                    {
                        //SwitchCell SWautoScroll = new SwitchCell();
                        //GDstatus.Children.Add(new TableView {Root=new TableRoot {Content=new TableSection { Content = { SWautoScroll} } } }, 1, 0);
                        //new TableRoot().
                        SWautoScroll = new MySwitch("Auto Scroll", "Manual Scroll") { IsToggled = true };
                        GDstatus.Children.Add(SWautoScroll, columnNumber++, 0);
                    }
                    {
                        BTNclear = new Button { Text = "Clear" };
                        GDstatus.Children.Add(BTNclear, columnNumber++, 0);
                    }
                    GDmain.Children.Add(GDstatus, 0, 1);
                }
                this.Content =new UnwipableContentView { Content = GDmain };
            }
        }
        //MyTextBox EDlog;
        LogPanel EDlog;
        MySwitch SWautoScroll;
        Grid GDmain, GDstatus;
        Button BTNclear;
        Label LBstatus;
    }
}
