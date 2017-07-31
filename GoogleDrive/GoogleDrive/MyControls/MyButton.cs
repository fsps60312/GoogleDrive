using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Forms;
using System.Threading.Tasks;

namespace GoogleDrive.MyControls
{
    class MyButton:Xamarin.Forms.Button
    {
        public async Task<bool> LayoutTo(Rectangle bounds, uint length = 250, Easing easing = null) { return await Xamarin.Forms.ViewExtensions.LayoutTo(this, bounds, length, easing); }
        public MyButton(string text)
        {
            this.Text = text;
        }
    }
}
