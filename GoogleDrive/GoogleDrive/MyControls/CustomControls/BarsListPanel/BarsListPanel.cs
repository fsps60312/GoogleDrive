using System;
using System.Collections.Generic;
using System.Text;
using GoogleDrive.MyControls;
using Xamarin.Forms;
using System.Threading.Tasks;

namespace GoogleDrive.MyControls.BarsListPanel
{
    public delegate void DataBindedViewEventHandler<T>(IDataBindedView<T> sender) where T : MyDisposable;
    public interface IDataBindedView<DataType> where DataType: MyDisposable
    {
        event DataBindedViewEventHandler<DataType> Appeared;
        void Reset(DataType data);
    }
    class BarsListPanel<GenericView,DataType>:MyContentView where DataType:MyDisposable where GenericView : Xamarin.Forms.View, IDataBindedView<DataType>,new()
    {
        Treap<DataType> treap = new Treap<DataType>();
        MyAbsoluteLayout ALmain;
        MyScrollView SVmain;
        MyLabel LBend;
        private delegate void TreapLayoutChangedEventHandler();
        private event TreapLayoutChangedEventHandler TreapLayoutChanged;
        private void OnTreapLayoutChanged() { TreapLayoutChanged?.Invoke(); }
        public void PushFront(DataType data)
        {
            var o=treap.Insert(data, 0);
            OnTreapLayoutChanged();
            data.Disposed += () =>
            {
                treap.Delete(o);
                OnTreapLayoutChanged();
            };
        }
        Stack<GenericView> AvaiableChildrenPool=new Stack<GenericView>();
        Dictionary<DataType, GenericView> ChildrenInUse = new Dictionary<DataType, GenericView>();
        GenericView GetGenericView()
        {
            if (AvaiableChildrenPool.Count == 0)
            {
                var c = new GenericView() { IsVisible = false, HorizontalOptions = Xamarin.Forms.LayoutOptions.FillAndExpand };
                AvaiableChildrenPool.Push(c);
                ALmain.Children.Add(c,new Rectangle(0,0,1,treap.itemHeight),AbsoluteLayoutFlags.WidthProportional);
            }
            var ans = AvaiableChildrenPool.Pop();
            ans.IsVisible = true;
            return ans;
        }
        private int UponIndex()
        {
            int l = 0, r = treap.Count - 1;
            while (l < r)
            {
                int mid = (l + r + 1) / 2;
                if (treap.Query(mid) > SVmain.ScrollY) r = mid-1;
                else l = mid;
            }
            MyLogger.Assert(l == r);
            return r;
        }
        private int DownIndex()
        {
            int l = 0, r = treap.Count - 1;
            while (l < r)
            {
                int mid = (l + r + 1) / 2;
                if (treap.Query(mid) >= SVmain.ScrollY + SVmain.Height) r = mid - 1;
                else l = mid;
            }
            MyLogger.Assert(l == r);
            return r;
        }
        volatile bool isLayoutRunning = false, needRunAgain = false;
        private bool UpdateLayout()
        {
            HashSet<DataType> remain = new HashSet<DataType>();
            foreach (var p in ChildrenInUse) remain.Add(p.Key);
            bool answer = false;
            treap.Query(treap.Count, new Action<Treap<DataType>.TreapNode>((o) =>
            {
                MyAbsoluteLayout.SetLayoutBounds(LBend, new Rectangle(0, o.QueryYOffset(), 1, -1));
                ALmain.HeightRequest = o.QueryYOffset() + treap.itemHeight;
            }));
            if (treap.Count > 0)
            {
                int l = UponIndex(), r = DownIndex();
                for (int i = l; i <= r; i++)
                {
                    treap.Query(i, new Action<Treap<DataType>.TreapNode>((o) =>
                    {
                        if (ChildrenInUse.ContainsKey(o.data))
                        {
                            MyAbsoluteLayout.SetLayoutBounds(ChildrenInUse[o.data], new Rectangle(0, o.QueryYOffset(), 1, -1));
                            remain.Remove(o.data);
                        }
                        else if (!answer)
                        {
                            var c = GetGenericView();
                            MyAbsoluteLayout.SetLayoutBounds(c, new Rectangle(0, o.QueryYOffset(), 1, -1));
                            c.Reset(o.data);
                            ChildrenInUse[o.data] = c;
                            answer = true;
                        }
                    }));
                }
            }
            foreach (var d in remain)
            {
                var v = ChildrenInUse[d];
                ChildrenInUse.Remove(d);
                v.Reset(null);
                v.IsVisible = false;
                AvaiableChildrenPool.Push(v);
            }
            return answer;
        }
        private void AnimateLayout()
        {
            if (isLayoutRunning)
            {
                needRunAgain = true;
                return;
            }
            isLayoutRunning = true;
            //ALmain.AbortAnimation("animation");
            //MyLogger.Log($"treap.Count: {treap.Count}");
            bool adding = UpdateLayout();
            ALmain.Animate("animation", new Animation(new Action<double>((ratio) =>
            {
                adding |= UpdateLayout();
            })), 16, (uint)Treap<DataType>.animationDuration,null,new Action<double, bool>((dv,bv)=>
            {
                adding |= UpdateLayout();
                isLayoutRunning = false;
                if (adding||needRunAgain)
                {
                    needRunAgain = false;
                    AnimateLayout();
                }
            }));
        }
        private void RegisterEvents()
        {
            this.TreapLayoutChanged += () => { AnimateLayout(); };
            SVmain.Scrolled += (sender,args) => { AnimateLayout(); };
            SVmain.SizeChanged += (sender, args) => { AnimateLayout(); };
        }
        private void InitializeViews()
        {
            {
                SVmain = new MyScrollView(Xamarin.Forms.ScrollOrientation.Vertical);
                {
                    ALmain = new MyAbsoluteLayout
                    {
                        HorizontalOptions = Xamarin.Forms.LayoutOptions.FillAndExpand,
                        VerticalOptions =Xamarin.Forms.LayoutOptions.Start,
                        HeightRequest = treap.itemHeight,
                        BackgroundColor = Xamarin.Forms.Color.LightYellow
                    };
                    {
                        LBend = new MyLabel("End of Results")
                        {
                            IsEnabled = false
                        };
                        ALmain.Children.Add(LBend,new Rectangle(0,0,1,treap.itemHeight),AbsoluteLayoutFlags.WidthProportional);
                    }
                    SVmain.Content = ALmain;
                }
                this.Content = SVmain;
            }
        }
        public BarsListPanel()
        {
            InitializeViews();
            RegisterEvents();
            OnTreapLayoutChanged();
            MyLogger.Test1 = new Func<Task>(async () =>
              {
                  var l = new MyLabel("I'm here") { BackgroundColor = Color.Red };
                  //await l.TranslateTo(0, SVmain.ScrollY);
                  //l.Layout(new Rectangle(0, SVmain.ScrollY, -1, -1));
                  //MyAbsoluteLayout.SetLayoutBounds(l, new Rectangle(0, SVmain.ScrollY, -1, -1));
                  ALmain.Children.Add(l,new Rectangle(-25,0,1,-1),AbsoluteLayoutFlags.WidthProportional);
                  await MyLogger.Alert("wait");
                  MyAbsoluteLayout.SetLayoutBounds(l, new Rectangle(0, SVmain.ScrollY, -1, -1));
                  int u = UponIndex(), d = DownIndex();
                  await MyLogger.Alert($"({SVmain.ScrollY},{SVmain.ScrollY + SVmain.Height}),({u},{d}),({treap.Query(u)},{treap.Query(d)}),({l.TranslationY},{l.Y},{l.TranslationY/l.Y},{l.Bounds})");
              });
            MyLogger.Test3 = new Func<Task>(async () =>
              {
                  await MyLogger.Alert($"AnimationIsRunning: {ALmain.AnimationIsRunning("animation")}");
              });
        }
    }
}
