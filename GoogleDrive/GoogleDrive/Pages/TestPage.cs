using System;
using System.Collections.Generic;
using System.Text;
using GoogleDrive.MyControls;

namespace GoogleDrive.Pages
{
    class TestPage:MyTabbedPage
    {
        public TestPage():base("Test Page")
        {
            this.Children.Add(new HttpRequestPage());
        }
    }
}
