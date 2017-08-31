using System;
using System.Collections.Generic;
using System.Text;
using GoogleDrive.MyControls;

namespace GoogleDrive
{
    class StatusPage:MyTabbedPage
    {
        public StatusPage():base("Status")
        {
            this.Children.Add(new FileTransferPage());
            this.Children.Add(new FileVerifyPage());
        }
    }
}
