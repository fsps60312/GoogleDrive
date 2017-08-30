using System;
using System.Collections.Generic;
using System.Text;

namespace GoogleDrive.MyControls
{
    class MySwitch :Xamarin.Forms.Switch
    {
        public MySwitch() : base() { }
        public MySwitch(string onText,string offText):this()
        {
        }
    }
    //class MySwitch:Xamarin.Forms.TableView
    //{
    //    Xamarin.Forms.SwitchCell switchCell;
    //    public bool IsToggled { get { return switchCell.On; } set { switchCell.On = value; } }
    //    public string Text { get { return switchCell.Text; } set { switchCell.Text = value; } }
    //    public event EventHandler<Xamarin.Forms.ToggledEventArgs> Toggled;
    //    public MySwitch()
    //    {
    //        var tableRoot = new Xamarin.Forms.TableRoot();
    //        var tableSelection = new Xamarin.Forms.TableSection();
    //        switchCell = new Xamarin.Forms.SwitchCell();
    //        switchCell.OnChanged += (sender,args)=> { Toggled?.Invoke(sender, args); };
    //        tableSelection.Add(switchCell);
    //        tableRoot.Add(tableSelection);
    //        this.Root = tableRoot;
    //    }
    //    public MySwitch(string onText, string offText) : this()
    //    {
    //        this.Text = (IsToggled ? onText : offText);
    //        this.Toggled += (sender, args) =>
    //        {
    //            this.Text = (IsToggled ? onText : offText);
    //        };
    //    }
    //}
}
