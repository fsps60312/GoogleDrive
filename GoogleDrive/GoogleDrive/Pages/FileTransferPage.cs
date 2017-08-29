using System;
using System.Collections.Generic;
using System.Text;
using GoogleDrive.MyControls;
using GoogleDrive.Pages.FileTransferPanels;

namespace GoogleDrive
{
    class FileTransferPage:MyContentPage
    {
        public FileTransferPage():base("File Transfer")
        {
            MyLogger.TestMethodAdded += (name, task) =>
              {
                  var ti = new MyToolbarItem(name);
                  ti.Clicked += async delegate { await task(); };
                  this.ToolbarItems.Add(ti);
              };
            //var test1 = new MyToolbarItem("test1");
            //var test2 = new MyToolbarItem("test2");
            //var test3 = new MyToolbarItem("test3");
            //var test4 = new MyToolbarItem("test4");
            //test1.Clicked += async delegate { await MyLogger.Test1(); };
            //test2.Clicked += async delegate { await MyLogger.Test2(); };
            //test3.Clicked += async delegate { await MyLogger.Test3(); };
            //test4.Clicked += async delegate { await MyLogger.Test4(); };
            //this.ToolbarItems.Add(test1);
            //this.ToolbarItems.Add(test2);
            //this.ToolbarItems.Add(test3);
            //this.ToolbarItems.Add(test4);
            this.Content = new FileTransferContentView();
        }
        //public FileTransferPage() : base("File Transfer")
        //{
        //    this.Children.Add()
        //    this.Content = new BasicStylePanel();
        //}
        //public FileTransferPage() : base("File Transfer")
        //{
        //    this.ToolbarItems.Add(new MyToolbarItem("test1"));
        //    this.ToolbarItems.Add(new MyToolbarItem("test2"));
        //    this.ToolbarItems.Add(new MyToolbarItem("test3"));
        //    this.ToolbarItems.Add(new MyToolbarItem("test4"));
        //    MyAbsoluteLayout al = new MyAbsoluteLayout();
        //    Xamarin.Forms.ListView lv = new Xamarin.Forms.ListView(Xamarin.Forms.ListViewCachingStrategy.RecycleElement);
        //    int mode = 0;
        //    var m = new MyButton(mode.ToString());
        //    m.Clicked += delegate
        //      {
        //          mode++;
        //          if (mode == 12) mode = 0;
        //          m.Text = mode.ToString();
        //      };
        //    bool hi = false;
        //    var b = new MyButton("I'm button");
        //    b.Clicked += async delegate
        //    {
        //        Xamarin.Forms.Easing easing = null;
        //        switch (mode)
        //        {
        //            case 0: easing = Xamarin.Forms.Easing.BounceIn; break;
        //            case 1: easing = Xamarin.Forms.Easing.BounceOut; break;
        //            case 2: easing = Xamarin.Forms.Easing.CubicIn; break;
        //            case 3: easing = Xamarin.Forms.Easing.CubicInOut; break;
        //            case 4: easing = Xamarin.Forms.Easing.CubicOut; break;
        //            case 5: easing = Xamarin.Forms.Easing.Linear; break;
        //            case 6: easing = Xamarin.Forms.Easing.SinIn; break;
        //            case 7: easing = Xamarin.Forms.Easing.SinInOut; break;
        //            case 8: easing = Xamarin.Forms.Easing.SinOut; break;
        //            case 9: easing = Xamarin.Forms.Easing.SpringIn; break;
        //            case 10: easing = Xamarin.Forms.Easing.SpringOut; break;
        //        }
        //        if (hi)
        //        {
        //            var r = await b.LayoutTo(new Xamarin.Forms.Rectangle(100, 50, 100, 50), 1000, easing);
        //            this.Title = r.ToString();
        //        }
        //        else
        //        {
        //            var r = await b.LayoutTo(new Xamarin.Forms.Rectangle(50, 100, 50, 100), 1000, easing);
        //            this.Title = r.ToString();
        //        }
        //        hi ^= true;
        //    };
        //    al.Children.Add(m,new Xamarin.Forms.Point(500,0));
        //    al.Children.Add(b);
        //    this.Content = new MyScrollView { Content = al };
        //}
    }
    class FileTransferContentView:GoogleDrive.MyControls.BarsListPanel.BarsListPanel<NetworkingItemBar, NetworkingItemBarViewModel>//BasicStylePanel
    {
        public FileTransferContentView()
        {
            CloudFile.Downloaders.FileDownloader.NewFileDownloadCreated += (networker) => { this.PushFront(new NetworkingItemBarViewModel(networker)); };
            CloudFile.Uploaders.FileUploader.NewFileUploadCreated += (networker) => { this.PushFront(new NetworkingItemBarViewModel(networker)); };
            CloudFile.Uploaders.FolderUploader.NewFolderUploadCreated += (networker) => { this.PushFront(new NetworkingItemBarViewModel(networker)); };
        }
    }
}
