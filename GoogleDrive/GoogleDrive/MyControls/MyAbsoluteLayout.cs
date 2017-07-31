using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace GoogleDrive.MyControls
{
    class MyAbsoluteLayout:Xamarin.Forms.AbsoluteLayout
    {
        public async Task<bool> LayoutTo(Rectangle bounds,uint length=250,Easing easing=null) { return await Xamarin.Forms.ViewExtensions.LayoutTo(this, bounds, length, easing); }
    }
}
