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
        class CloudFolderContentPanel : MyStackPanel
        {
            CloudFile cloudFolder;
            ActivityIndicator PBmain;
            Button BTNrefresh;
            MyLabel LBselected = null;
            public int FolderDepth;
            public CloudFolderContentPanel(CloudFile _cloudFolder, int folderDepth) : base(ScrollOrientation.Vertical)
            {
                MyLogger.Assert(_cloudFolder.IsFolder);
                cloudFolder = _cloudFolder;
                FolderDepth = folderDepth;
                this.BackgroundColor = Color.LightGoldenrodYellow;
                {
                    BTNrefresh = new Button { Text = "↻", BackgroundColor = Color.YellowGreen };
                    BTNrefresh.Clicked += async delegate
                    {
                        BTNrefresh.IsEnabled = false;
                        await RefreshContent();
                        BTNrefresh.IsEnabled = true;
                    };
                    this.Children.Add(BTNrefresh);
                }
                {
                    PBmain = new ActivityIndicator { IsRunning = IsVisible = true };
                    this.Children.Add(PBmain);
                }
            }
            public async Task RefreshContent()
            {
                PBmain.IsRunning = PBmain.IsVisible = true;
                var folderList = await cloudFolder.GetFoldersAsync();
                this.Children.Clear();
                this.Children.Add(BTNrefresh);
                this.Children.Add(PBmain);
                foreach (var subFolder in folderList)
                {
                    var lb = new CloudFileLabel(subFolder);
                    lb.FileClicked += delegate
                    {
                        LBselected?.Deselect();
                        (LBselected = lb).Select();
                        OnFileClicked(lb);
                    };
                    this.Children.Add(lb);
                }
                var fileList = await cloudFolder.GetFilesAsync();
                //if (fileList.Count > 10) fileList = fileList.GetRange(0, 10);
                foreach (var file in fileList)
                {
                    var lb = new CloudFileLabel(file);
                    lb.FileClicked += delegate
                    {
                        LBselected?.Deselect();
                        (LBselected = lb).Select();
                        OnFileClicked(lb);
                    };
                    this.Children.Add(lb);
                }
                PBmain.IsRunning = PBmain.IsVisible = false;
                MyLogger.Log("Folders loaded");
                await Task.Delay(500);
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
                    SPpanel.Children.Remove(LBpadding);
                    SPpanel.Children.Add(cfcp);
                    SPpanel.Children.Add(LBpadding);
                    cfcp.FileClicked += async delegate (CloudFileLabel label)
                    {
                        foreach (var p in Stack.GetRange(cfcp.FolderDepth, Stack.Count - cfcp.FolderDepth))
                        {
                            SPpanel.Children.Remove(p);
                        }
                        Stack.RemoveRange(cfcp.FolderDepth, Stack.Count - cfcp.FolderDepth);
                        if (label.File.IsFolder)
                        {
                            await PushStack(label.File);
                        }
                    };
                    await cfcp.RefreshContent();
                    await SPpanel.ScrollToAsync(cfcp, ScrollToPosition.Center, true);
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
