using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace GoogleDrive
{
    class UnwipableContentView : ContentView
    {
        public UnwipableContentView()
        {
            this.GestureRecognizers.Add(new PinchGestureRecognizer());
        }
    }
    class MyLabel : Button
    {
        //Color preColor;
        double Add(double a, double b) { return Math.Min(1.0, Math.Max(0.0, a + b)); }
        public void BigSelect()
        {
            var c = this.BackgroundColor;
            double d = -0.2;
            this.BackgroundColor = new Color(Add(c.R, d), Add(c.G, d), Add(c.B, d));
        }
        public void Select()
        {
            //preColor = this.BackgroundColor;
            var c = this.BackgroundColor;
            double d = -0.2;
            this.BackgroundColor = new Color(Add(c.R, d), Add(c.G, d), Add(c.B, d));
        }
        public void Deselect()
        {
            //this.BackgroundColor = preColor;
            base.BackgroundColor = colorSet[colorSet.Count - 1];
            colorSet.RemoveAt(colorSet.Count - 1);
        }
        List<Color> colorSet = new List<Color>();
        public new Color BackgroundColor
        {
            get
            {
                return base.BackgroundColor;
            }
            set
            {
                colorSet.Add(base.BackgroundColor);
                base.BackgroundColor = value;
            }
        }
        public MyLabel(string text)
        {
            this.Text = text;
            this.FontSize = 15;
            //this.Padding = new Thickness(15, 5, 15, 5);
        }
    }
    class CloudFileExplorePanel : UnwipableContentView
    {
        class MarginedStackPanel : StackLayout
        {
            public MarginedStackPanel(StackOrientation orientation)
            {
                this.Orientation = orientation;
            }
        }
        class MyStackPanel : ScrollView
        {
            MarginedStackPanel SPmain;
            public MyStackPanel(ScrollOrientation orientation)
            {
                this.Orientation = orientation;
                {
                    switch (this.Orientation)
                    {
                        case ScrollOrientation.Horizontal:
                            SPmain = new MarginedStackPanel(StackOrientation.Horizontal);
                            break;
                        case ScrollOrientation.Vertical:
                            SPmain = new MarginedStackPanel(StackOrientation.Vertical);
                            break;
                    }
                    this.Content = SPmain;
                }
            }
            public IList<View> Children
            {
                get { return SPmain.Children; }
            }
        }
        class CloudFileLabel : MyLabel
        {
            public CloudFile File { get; private set; }
            public CloudFileLabel(CloudFile file) : base(file.Name)
            {
                this.File = file;
                if(file.IsFolder) this.BackgroundColor = Color.GreenYellow;
                else this.BackgroundColor = Color.Yellow;
                this.Clicked += CloudFileLabel_Clicked;
            }
            DateTime lastClick = DateTime.MinValue;
            private async void CloudFileLabel_Clicked(object sender, EventArgs e)
            {
                if (lastClick == DateTime.MinValue || (DateTime.Now - lastClick).TotalMilliseconds > 500)
                {
                    lastClick = DateTime.Now;
                    OnFileClicked(this);
                }
                else
                {
                    this.IsEnabled = false;
                    await MyLogger.Alert($"Id: {File.Id}");
                    this.IsEnabled = true;
                }
            }
            public delegate void FileClickedEventHandler(CloudFileLabel label);
            public event FileClickedEventHandler FileClicked;
            private void OnFileClicked(CloudFileLabel label) { FileClicked?.Invoke(label); }
        }
        class CloudFolderContentPanel : Grid
        {
            CloudFile cloudFolder;
            ActivityIndicator PBmain;
            MyStackPanel SPcontent;
            Button BTNrefresh;
            MyLabel LBselected = null;
            CloudFile.SearchListGetter FoldersGetter,FilesGetter;
            public int FolderDepth;
            public CloudFolderContentPanel(CloudFile _cloudFolder, int folderDepth)
            {
                MyLogger.Assert(_cloudFolder.IsFolder);
                cloudFolder = _cloudFolder;
                FolderDepth = folderDepth;
                FoldersGetter = cloudFolder.FoldersGetter();
                FilesGetter = cloudFolder.FilesGetter();
                this.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                this.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                this.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                this.BackgroundColor = Color.LightGoldenrodYellow;
                this.MinimumWidthRequest = 100;
                {
                    BTNrefresh = new Button { Text = "↻", BackgroundColor = Color.YellowGreen };
                    BTNrefresh.Clicked += async delegate
                    {
                        BTNrefresh.IsEnabled = false;
                        await RefreshContent();
                        BTNrefresh.IsEnabled = true;
                    };
                    this.Children.Add(BTNrefresh, 0, 0);
                }
                {
                    SPcontent = new MyStackPanel(ScrollOrientation.Vertical);
                    this.Children.Add(SPcontent, 0, 1);
                }
                {
                    PBmain = new ActivityIndicator { IsRunning = IsVisible = true };
                    this.Children.Add(PBmain, 0, 2);
                }
            }
            bool StopRequest,IsRunning=false;
            public async Task RefreshContent()
            {
                PBmain.IsRunning = PBmain.IsVisible = true;
                StopRequest = false;
                IsRunning = true;
                await FoldersGetter.ResetAsync();
                var list =await FoldersGetter.GetNextPageAsync();
                SPcontent.Children.Clear();
                while (list != null)
                {
                    foreach (var subFolder in list)
                    {
                        if (StopRequest)
                        {
                            IsRunning = false;
                            return;
                        }
                        var lb = new CloudFileLabel(subFolder);
                        lb.FileClicked += delegate
                        {
                            LBselected?.Deselect();
                            (LBselected = lb).Select();
                            OnFileClicked(lb);
                        };
                        SPcontent.Children.Add(lb);
                    }
                    list = await FoldersGetter.GetNextPageAsync();
                }
                await FilesGetter.ResetAsync();
                list = await FilesGetter.GetNextPageAsync();
                while (list != null)
                {
                    foreach (var file in list)
                    {
                        if (StopRequest)
                        {
                            IsRunning = false;
                            return;
                        }
                        var lb = new CloudFileLabel(file);
                        lb.FileClicked += delegate
                        {
                            LBselected?.Deselect();
                            (LBselected = lb).Select();
                            OnFileClicked(lb);
                        };
                        SPcontent.Children.Add(lb);
                    }
                    list = await FilesGetter.GetNextPageAsync();
                }
                PBmain.IsRunning = PBmain.IsVisible = false;
                MyLogger.Log("Folders loaded");
                IsRunning = false;
                await Task.Delay(500);
            }
            public async Task StopRefreshing()
            {
                if(IsRunning)
                {
                    StopRequest = true;
                    while (IsRunning) await Task.Delay(100);
                }
            }
            public event CloudFileLabel.FileClickedEventHandler FileClicked;
            private void OnFileClicked(CloudFileLabel label) { FileClicked?.Invoke(label); }
        }
        class CloudFolderStackPanel : ContentView
        {
            MyStackPanel SPpanel;
            List<CloudFolderContentPanel> Stack = new List<CloudFolderContentPanel>();
            Label LBpadding;
            public CloudFolderStackPanel()
            {
                InitializaControls();
                DoAsyncInitializationTasks();
            }
            private async void DoAsyncInitializationTasks()
            {
                await PushStack(CloudFile.RootFolder);
            }
            private async Task PushStack(CloudFile cloudFolder)
            {
                try
                {
                    MyLogger.Assert(cloudFolder.IsFolder);
                    CloudFolderContentPanel cfcp = new CloudFolderContentPanel(cloudFolder, Stack.Count + 1);
                    cfcp.FileClicked += delegate (CloudFileLabel label) { OnFileClicked(label); };
                    Stack.Add(cfcp);
                    SPpanel.Children.Add(cfcp);
                    cfcp.FileClicked += async delegate (CloudFileLabel label)
                    {
                        foreach (var p in Stack.GetRange(cfcp.FolderDepth, Stack.Count - cfcp.FolderDepth))
                        {
                            await p.StopRefreshing();
                            SPpanel.Children.Remove(p);
                        }
                        Stack.RemoveRange(cfcp.FolderDepth, Stack.Count - cfcp.FolderDepth);
                        if (label.File.IsFolder)
                        {
                            await PushStack(label.File);
                        }
                    };
                    await cfcp.RefreshContent();
                    await SPpanel.ScrollToAsync(double.MaxValue, 0, true);
                    await SPpanel.ScrollToAsync(double.MaxValue, 0, false);
                }
                catch(Exception error)
                {
                    await MyLogger.Alert(error.ToString());
                }
            }
            public event CloudFileLabel.FileClickedEventHandler FileClicked;
            private void OnFileClicked(CloudFileLabel label) { FileClicked?.Invoke(label); }
            private void InitializaControls()
            {
                {
                    SPpanel = new MyStackPanel(ScrollOrientation.Horizontal) { BackgroundColor = Color.LightYellow };// {VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch };
                    {
                        LBpadding = new Label { IsVisible = true, WidthRequest = 0 };
                        SPpanel.Children.Add(LBpadding);
                    }
                    this.Content = SPpanel;
                }
            }
        }
        public CloudFileExplorePanel()
        {
            {
                CFSPmain = new CloudFolderStackPanel();
                CFSPmain.FileClicked += delegate (CloudFileLabel label)
                {
                    if (ItemSelected != null) ItemSelected.Deselect();
                    label.BigSelect();
                    ItemSelected = label;
                    OnSelectedFileChanged(label.File);
                };
                this.Content = CFSPmain;
            }
        }
        public delegate void SelectedFileChangedEventHandler(CloudFile file);
        public event SelectedFileChangedEventHandler SelectedFileChanged;
        private void OnSelectedFileChanged(CloudFile file) { SelectedFileChanged?.Invoke(file); }
        CloudFileLabel ItemSelected = null;
        CloudFolderStackPanel CFSPmain;
    }
}
