﻿using System;
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
        Func<Task> Disappearing { get; set; }
        void Reset(DataType data);
    }
    class BarsListPanel<GenericView,DataType>:MyContentView where DataType:MyDisposable where GenericView : Xamarin.Forms.View, IDataBindedView<DataType>,new()
    {
        public delegate void DataEventHandler(DataType data);
        public event DataEventHandler DataInserted, DataRemoved;
        private void OnDataInserted(DataType data) { DataInserted?.Invoke(data); }
        private void OnDataRemoved(DataType data) { DataRemoved?.Invoke(data); }
        protected double AnimationDuration { get { return Treap<DataType>.animationDuration; } }
        protected double ItemHeight
        {
            get { return treap.itemHeight; }
            set { treap.itemHeight = value; }
        }
        public Treap<DataType> Treap
        {
            get { return treap; }
        }
        Treap<DataType> treap = new Treap<DataType>();
        MyAbsoluteLayout ALmain;
        protected MyScrollView SVmain;
        MyLabel LBend;
        private delegate void TreapLayoutChangedEventHandler();
        private event TreapLayoutChangedEventHandler TreapLayoutChanged;
        private void OnTreapLayoutChanged() { TreapLayoutChanged?.Invoke(); }
        protected void ChangeHeight(Treap<DataType>.TreapNode node, double difference)
        {
            treap.ChangeHeight(node, difference);
        }
        public void Clear()
        {
            treap.DisposeAll();
        }
        private void RegisterData(Treap<DataType>.TreapNode o, DataType data)
        {
            MyDisposable.MyDisposableEventHandler disposedEventHandler = null;
            MyDisposable.HeightChangedEventHandler heightChangedEventHandler = null;
            disposedEventHandler = new MyDisposable.MyDisposableEventHandler(() =>
            {
                data.Disposed -= disposedEventHandler;
                data.HeightChanged -= heightChangedEventHandler;
                treap.Delete(o);
                OnTreapLayoutChanged();
                OnDataRemoved(data);
            });
            heightChangedEventHandler = new MyDisposable.HeightChangedEventHandler((difference) =>
              {
                  treap.ChangeHeight(o, difference);
                  OnTreapLayoutChanged();
              });
            data.Disposed += disposedEventHandler;
            data.HeightChanged += heightChangedEventHandler;
            OnDataInserted(data);
        }
        public void PushFront(DataType data)
        {
            var o = treap.Insert(data, 0);
            RegisterData(o, data);
            OnTreapLayoutChanged();
        }
        public void PushBack(DataType data)
        {
            var o = treap.Insert(data, treap.Count);
            RegisterData(o, data);
            OnTreapLayoutChanged();
        }
        public async Task ScrollToEnd()
        {
            await SVmain.ScrollToAsync(0, double.MaxValue, true);
            await SVmain.ScrollToAsync(0, double.MaxValue, false);
        }
        Stack<GenericView> AvaiableChildrenPool=new Stack<GenericView>();
        Dictionary<DataType, GenericView> ChildrenInUse = new Dictionary<DataType, GenericView>();
        public Func<double, Tuple<Rectangle, AbsoluteLayoutFlags>> BarsLayoutMethod = null;
            //new Func<double, Tuple<Rectangle, AbsoluteLayoutFlags>>((y) =>
            //{
            //    return new Tuple<Rectangle, AbsoluteLayoutFlags>(new Rectangle(0, y, 1, -1), AbsoluteLayoutFlags.WidthProportional);
            //});
        GenericView GetGenericView()
        {
            if (AvaiableChildrenPool.Count == 0)
            {
                var c = new GenericView() { IsVisible = false, HorizontalOptions = Xamarin.Forms.LayoutOptions.FillAndExpand };
                AvaiableChildrenPool.Push(c);
                var layoutMethod = BarsLayoutMethod(0);
                ALmain.Children.Add(c, layoutMethod.Item1, layoutMethod.Item2);
            }
            var ans = AvaiableChildrenPool.Pop();
            ans.IsVisible = true;
            return ans;
        }
        private int UponIndex()
        {
            return Math.Max(0, Math.Min(treap.Count - 1, treap.QueryLowerBound(SVmain.ScrollY) - 1));
            //int l = 0, r = treap.Count - 1;
            //while (l < r)
            //{
            //    int mid = (l + r + 1) / 2;
            //    if (treap.Query(mid) > SVmain.ScrollY) r = mid-1;
            //    else l = mid;
            //}
            //MyLogger.Assert(l == r);
            //return r;
        }
        private int DownIndex()
        {
            return Math.Max(0, Math.Min(treap.Count - 1, treap.QueryLowerBound(SVmain.ScrollY + SVmain.Height) - 1));
            //int l = 0, r = treap.Count - 1;
            //while (l < r)
            //{
            //    int mid = (l + r + 1) / 2;
            //    if (treap.Query(mid) >= SVmain.ScrollY + SVmain.Height) r = mid - 1;
            //    else l = mid;
            //}
            //MyLogger.Assert(l == r);
            //return r;
        }
        volatile bool isLayoutRunning = false, needRunAgain = false;
        private bool UpdateLayout()
        {
            HashSet<DataType> remain = new HashSet<DataType>();
            foreach (var p in ChildrenInUse) remain.Add(p.Key);
            bool answer = false;
            SVmain.BatchBegin();
            treap.Query(treap.Count, new Action<Treap<DataType>.TreapNode>((o) =>
            {
                MyAbsoluteLayout.SetLayoutBounds(LBend, BarsLayoutMethod(o.QueryYOffset()).Item1);
                ALmain.HeightRequest = o.QueryYOffset() + treap.itemHeight;
            }));
            if (treap.Count > 0)
            {
                double difference = 0;
                int l = UponIndex(), r = DownIndex();
                for (int i = l; i <= r; i++)
                {
                    treap.Query(i, new Action<Treap<DataType>.TreapNode>((o) =>
                    {
                        var targetBound = BarsLayoutMethod(o.QueryYOffset());
                        GenericView view = null;
                        if (ChildrenInUse.ContainsKey(o.data))
                        {
                            view = ChildrenInUse[o.data];
                            remain.Remove(o.data);
                            if (i == (l + r) / 2 && view.Bounds != null) difference = targetBound.Item1.Y - view.Bounds.Y;
                        }
                        else if (!answer)
                        {
                            answer = true;
                            view = GetGenericView();
                            view.Reset(o.data);
                            ChildrenInUse[o.data] = view;
                        }
                        if (view != null)
                        {
                            MyAbsoluteLayout.SetLayoutBounds(view, targetBound.Item1);
                        }
                    }));
                }
                if (difference != 0)
                {
                    SVmain.MyScrollY += difference;
                    //SVmain.ScrollToAsync(SVmain.ScrollX, SVmain.ScrollY + difference /** 1.05*/, false);
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
            SVmain.BatchCommit();
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
                        var layoutMethod = BarsLayoutMethod(0);
                        ALmain.Children.Add(LBend,layoutMethod.Item1,layoutMethod.Item2);
                    }
                    SVmain.Content = ALmain;
                }
                this.Content = SVmain;
            }
        }
        public BarsListPanel()
        {
            BarsLayoutMethod = new Func<double, Tuple<Rectangle, AbsoluteLayoutFlags>>((y) =>
           {
               return new Tuple<Rectangle, AbsoluteLayoutFlags>(new Rectangle(0, y, ALmain.Width, -1), AbsoluteLayoutFlags.None);
           });
            InitializeViews();
            RegisterEvents();
            OnTreapLayoutChanged();
            //MyLogger.AddTestMethod("Show scroll info", new Func<Task>(async () =>
            //  {
            //      var l = new MyLabel("I'm here") { BackgroundColor = Color.Red };
            //      //await l.TranslateTo(0, SVmain.ScrollY);
            //      //l.Layout(new Rectangle(0, SVmain.ScrollY, -1, -1));
            //      //MyAbsoluteLayout.SetLayoutBounds(l, new Rectangle(0, SVmain.ScrollY, -1, -1));
            //      ALmain.Children.Add(l,new Rectangle(-25,0,1,-1),AbsoluteLayoutFlags.WidthProportional);
            //      await MyLogger.Alert("wait");
            //      MyAbsoluteLayout.SetLayoutBounds(l, new Rectangle(0, SVmain.ScrollY, -1, -1));
            //      int u = UponIndex(), d = DownIndex();
            //      await MyLogger.Alert($"({SVmain.ScrollY},{SVmain.ScrollY + SVmain.Height}),({u},{d}),({treap.Query(u)},{treap.Query(d)}),({l.TranslationY},{l.Y},{l.TranslationY/l.Y},{l.Bounds})");
            //  }));
            //MyLogger.AddTestMethod("AnimationIsRunning", new Func<Task>(async () =>
            //  {
            //      await MyLogger.Alert($"AnimationIsRunning: {ALmain.AnimationIsRunning("animation")}");
            //  }));
        }
    }
}
