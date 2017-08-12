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
    class ProcessingPanel : MyContentView
    {
        MyStackPanel ALmain;
        public ProcessingPanel()
        {
            //{
            //    ALmain = new MyStackPanel(Xamarin.Forms.ScrollOrientation.Vertical);
            //    this.Content = ALmain;
            //}
            {
                MyGrid g = new MyGrid();
                g.RowDefinitions.Add(new Xamarin.Forms.RowDefinition { Height = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Auto) });
                g.RowDefinitions.Add(new Xamarin.Forms.RowDefinition { Height = new Xamarin.Forms.GridLength(1, Xamarin.Forms.GridUnitType.Star) });
                MyButton b = new MyButton("hi");
                b.Clicked +=  delegate
                 {
                     MyLogger.Log(Newtonsoft.Json.JsonConvert.SerializeObject(ALmain.ScrollY));
                 };
                g.Children.Add(b, 0, 0);
                g.Children.Add(ALmain = new MyStackPanel(Xamarin.Forms.ScrollOrientation.Vertical), 0, 1);
                this.Content = g;
                for(int i=0;i<100;i++)
                {
                    ALmain.Children.Add(new MyButton($"la #{i}"));
                }
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
