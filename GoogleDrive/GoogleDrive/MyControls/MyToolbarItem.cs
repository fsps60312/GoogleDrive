using System;
using System.Collections.Generic;
using System.Text;

namespace GoogleDrive.MyControls
{
    class MyToolbarItem:Xamarin.Forms.ToolbarItem
    {
        public MyToolbarItem(string name)
        {
            this.Text = name;
            this.Icon = "";
        }
    }
}
