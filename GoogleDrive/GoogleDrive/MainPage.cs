using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace GoogleDrive
{
    class MainPage:TabbedPage
    {
        public MainPage()
        {
            {
                PGlog = new LogPage();
                this.Children.Add(PGlog);
            }
            {
                PGfileTransfer = new FileTransferPage();
                this.Children.Add(PGfileTransfer);
            }
            {
                PGtest = new Test.TestPage();
                this.Children.Add(PGtest);
            }
        }

        LogPage PGlog;
        FileTransferPage PGfileTransfer;
        Test.TestPage PGtest;
    }
}
