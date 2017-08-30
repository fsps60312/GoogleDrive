using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Forms;
using System.Threading.Tasks;

namespace GoogleDrive.MyControls
{
    class MySwitch :Xamarin.Forms.Grid
    {
        Switch SWmain;
        public event EventHandler<ToggledEventArgs> Toggled;
        public bool IsToggled
        {
            get { return SWmain.IsToggled; }
            set { SWmain.IsToggled = value; }
        }
        public MySwitch() : base()
        {
            this.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            {
                SWmain = new Switch();
                SWmain.Toggled += (sender, e) => { Toggled?.Invoke(sender, e); };
                this.Children.Add(SWmain, 0, 0);
            }
        }
        public MySwitch(string onText,string offText,bool onLeft=true):this()
        {
            {
                MyLabel lbl = new MyLabel(offText) { IsVisible = false, Opacity = 0, FontAttributes = FontAttributes.Bold, VerticalTextAlignment = TextAlignment.Center };
                if (onLeft)
                {
                    MyGrid.SetColumn(SWmain, 1);
                    this.Children.Add(lbl, 0, 0);
                }
                else
                {
                    this.Children.Add(lbl, 1, 0);
                }
                System.Threading.SemaphoreSlim semaphoreSlim = new System.Threading.SemaphoreSlim(1, 1);
                bool animationCompletedWith = false;
                SWmain.Toggled += async delegate
                {
                    bool backUp = SWmain.IsToggled;
                    try
                    {
                        await semaphoreSlim.WaitAsync();
                        lbl.Text = (SWmain.IsToggled ? onText : offText);
                        if (backUp != SWmain.IsToggled || animationCompletedWith == backUp) return;
                        lbl.IsVisible = true;
                        await lbl.FadeTo(1);
                        await Task.Delay(1000);
                        await lbl.FadeTo(0);
                        lbl.IsVisible = false;
                        animationCompletedWith = backUp;
                    }
                    finally
                    {
                        lock (semaphoreSlim) semaphoreSlim.Release();
                    }
                };
            }
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
