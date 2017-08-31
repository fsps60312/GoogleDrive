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
                PGfileBrowse = new FileBrowsePage();
                this.Children.Add(PGfileBrowse);
            }
            {
                PGfileTransfer = new StatusPage();
                //PGfileTransfer.Icon = "StoreLogo.png";
                this.Children.Add(new NavigationPage(PGfileTransfer) { Title = PGfileTransfer.Title });
                //PGfileTransfer.Title = null;
            }
            //{
            //    this.Children.Add(new FileTransferPage { Title = "|", Icon = "StoreLogo" });
            //}
        }

        LogPage PGlog;
        FileBrowsePage PGfileBrowse;
        StatusPage PGfileTransfer;
    }
}
