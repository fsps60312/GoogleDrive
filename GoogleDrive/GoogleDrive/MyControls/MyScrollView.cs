using System;
using System.Collections.Generic;
using System.Text;

namespace GoogleDrive.MyControls
{
    class MyScrollView:Xamarin.Forms.ScrollView
    {
        public MyScrollView(Xamarin.Forms.ScrollOrientation scrollOrientation)
        {
            this.Orientation = scrollOrientation;
        }
    }
}
