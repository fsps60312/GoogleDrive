using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using Xamarin.Forms;

namespace GoogleDrive.MyControls
{
    class MyScrollView : Xamarin.Forms.ScrollView
    {
        //class ScrollViewBinding : INotifyPropertyChanged
        //{
        //    public event PropertyChangedEventHandler PropertyChanged;
        //    private void OnPropertyChanged(string propertyName)
        //    {
        //        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        //    }
        //    private double __MyScrollY__ = 0;
        //    public double MyScrollY
        //    {
        //        get { return __MyScrollY__; }
        //        set
        //        {
        //            __MyScrollY__ = value;
        //            OnPropertyChanged("MyScrollY");
        //        }
        //    }
        //}
        public double MyScrollY
        {
            get { return this.ScrollY; }
            set {/*Currently no good solution to this, that is no gurrenteed to be executed -> */ /*this.ScrollToAsync(0, value, false); */}
        }
        //ScrollViewBinding scrollViewBinding = new ScrollViewBinding();
        public MyScrollView(Xamarin.Forms.ScrollOrientation scrollOrientation)
        {
            this.Orientation = scrollOrientation;
            //this.SetBinding(MyScrollView.ScrollYProperty, "MyScrollY", BindingMode.TwoWay);//, BindingMode.TwoWay);
            //this.BindingContext = scrollViewBinding;
        }
    }
}
