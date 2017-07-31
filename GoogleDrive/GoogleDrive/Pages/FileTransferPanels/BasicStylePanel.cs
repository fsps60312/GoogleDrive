using System;
using System.Collections.Generic;
using System.Text;
using GoogleDrive.MyControls;

namespace GoogleDrive.Pages.FileTransferPanels
{
    class BasicStylePanel:MyTabbedPage
    {
        public BasicStylePanel():base("File Transfer")
        {
            this.Children.Add(new MyContentPage("Processing") { Content = new ProcessingPanel() });
            this.Children.Add(new MyContentPage("Completed") { Content = new ProcessingPanel() });
        }
    }
    class ProcessingPanel : MyScrollView
    {
        MyStackPanel ALmain;
        public ProcessingPanel() : base(Xamarin.Forms.ScrollOrientation.Vertical)
        {
            {
                ALmain = new MyStackPanel(Xamarin.Forms.ScrollOrientation.Vertical);
                this.Content = ALmain;
            }
            CloudFile.Downloaders.FileDownloader.NewDownloadCreated += FileDownloader_NewDownloadCreated;
            CloudFile.Uploaders.FileUploader.NewUploadCreated += FileUploader_NewUploadCreated;
        }

        private void FileUploader_NewUploadCreated(CloudFile.Uploaders.FileUploader uploader)
        {
            this.Children.Add(new  NetworkingItemBar(uploader));
                //Xamarin.Forms.Constraint.Constant(0),
                //Xamarin.Forms.Constraint.Constant((cnt++) * 30),
                //Xamarin.Forms.Constraint.RelativeToParent((parent) => parent.Width));
        }

        ~ProcessingPanel()
        {
            CloudFile.Downloaders.FileDownloader.NewDownloadCreated -= FileDownloader_NewDownloadCreated;
            CloudFile.Uploaders.FileUploader.NewUploadCreated -= FileUploader_NewUploadCreated;
        }
        int cnt = 0;
        private void FileDownloader_NewDownloadCreated(CloudFile.Downloaders.FileDownloader downloader)
        {
            this.Children.Add(new NetworkingItemBar(downloader));
                //Xamarin.Forms.Constraint.Constant(0),
                //Xamarin.Forms.Constraint.Constant((cnt++) * 30),
                //Xamarin.Forms.Constraint.RelativeToParent((parent) => parent.Width));
        }

        public IList<Xamarin.Forms.View> Children
        {
            get { return ALmain.Children; }
        }
    }
}
