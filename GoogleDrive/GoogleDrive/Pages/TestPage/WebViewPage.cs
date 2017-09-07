using System;
using System.Collections.Generic;
using System.Text;
using GoogleDrive.MyControls;

namespace GoogleDrive.Pages
{
    class WebViewPage:MyContentPage
    {
        MyGrid GDmain;
        MyWebView WVmain;
        public WebViewPage():base("Web View")
        {
            {
                GDmain = new MyGrid();
                {
                    WVmain = new MyWebView();
                    WVmain.Source = "https://codingsimplifylife.blogspot.tw/";
                    GDmain.Children.Add(WVmain, 0, 0);
                }
                this.Content = GDmain;
            }
        }
    }
}
