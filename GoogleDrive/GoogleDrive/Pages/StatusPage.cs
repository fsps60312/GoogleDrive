using System;
using System.Collections.Generic;
using System.Text;
using GoogleDrive.MyControls;

namespace GoogleDrive.Pages
{
    class StatusPage:MyTabbedPage
    {
        EventHandler initializeThis;
        public StatusPage():base("Status")
        {
            this.Appearing += (initializeThis = new EventHandler(delegate
                {
                    this.Appearing -= initializeThis;
                    this.Children.Add(new FileTransferPage());
                    this.Children.Add(new FileVerifyPage());
                }));
        }
    }
}
