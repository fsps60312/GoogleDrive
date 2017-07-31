using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Forms;

namespace GoogleDrive.MyControls
{
    class MyLabel:Xamarin.Forms.Label
    {
        public MyLabel(string text)
        {
            this.LineBreakMode = LineBreakMode.NoWrap;
            this.Text = text;
        }
    }
}
