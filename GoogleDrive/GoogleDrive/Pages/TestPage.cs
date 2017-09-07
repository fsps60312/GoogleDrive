using System;
using System.Collections.Generic;
using System.Text;
using GoogleDrive.MyControls;

namespace GoogleDrive.Pages
{
    class TestPage:MyTabbedPage
    {
        EventHandler initializeThis;
        public TestPage():base("Test Page")
        {
            this.Appearing += (initializeThis = new EventHandler(delegate
            {
                this.Appearing -= initializeThis;
                this.Children.Add(new HttpRequestPage());
                this.Children.Add(new WebViewPage());
            }));
        }
    }
}
