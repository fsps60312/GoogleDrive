using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Forms;

namespace GoogleDrive
{
    class MarginedStackPanel : StackLayout
    {
        public MarginedStackPanel(StackOrientation orientation)
        {
            this.Orientation = orientation;
        }
    }
    class MyStackPanel : ScrollView
    {
        MarginedStackPanel SPmain;
        public MyStackPanel(ScrollOrientation orientation)
        {
            this.Orientation = orientation;
            {
                switch (this.Orientation)
                {
                    case ScrollOrientation.Horizontal:
                        SPmain = new MarginedStackPanel(StackOrientation.Horizontal);
                        break;
                    case ScrollOrientation.Vertical:
                        SPmain = new MarginedStackPanel(StackOrientation.Vertical);
                        break;
                }
                this.Content = SPmain;
            }
        }
        public IList<View> Children
        {
            get { return SPmain.Children; }
        }
    }
}
