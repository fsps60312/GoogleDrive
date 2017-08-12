using System;
using System.Collections.Generic;
using System.Text;
using GoogleDrive.MyControls;
using Xamarin.Forms;

namespace GoogleDrive.MyControls.BarsListPanel
{
    public class Treap<DataType>
    {
        public class TreapNode<DataType1>
        {
            public DataType1 data;
            //private TreapNode<DataType1> l
            //{
            //    get { return _l; }
            //    set
            //    {
            //        if (_l != null) _l.parent = null;
            //        _l = value;
            //        if (_l != null) _l.parent = this;
            //    }
            //}
            //private TreapNode<DataType1> r
            //{
            //    get { return _r; }
            //    set
            //    {
            //        if (_r != null) _r.parent = null;
            //        _r = value;
            //        if (_r != null) _r.parent = this;
            //    }
            //}
            private TreapNode<DataType1> l = null, r = null, parent = null;
            private uint priority = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0);
            private double yOffset, yOffsetTag = 0;
            private DateTime animationStartTime = DateTime.MinValue, animationStartTimeTag = DateTime.MinValue;
            private double animationOffset, animationOffsetTag;
            private int size = 1;
            public TreapNode(DataType1 _data, double _yOffset)
            {
                data = _data;
                yOffset = _yOffset;
            }
            private void Maintain()
            {
                size = GetSize(l) + 1 + GetSize(r);
            }
            private double AppendAnimation(ref DateTime time1, ref double offset1, DateTime time2, double offset2)
            {
                if (time1 == DateTime.MinValue)
                {
                    time1 = time2;
                    offset1 = offset2;
                    return 0;
                }
                else
                {
                    double ratio = AnimationOffsetRatio((time2 - time1).TotalMilliseconds / animationDuration);
                    double moved = offset1 * ratio;
                    time1 = time2;
                    offset1 = (offset1 - moved) + offset2;
                    return moved;
                }
            }
            private void PutDown(TreapNode<DataType1> child)
            {
                if (child == null) return;
                child.yOffset += this.yOffsetTag;
                child.yOffsetTag += this.yOffsetTag;
                //this.yOffsetTag = 0;
                if (animationStartTimeTag != DateTime.MinValue)
                {
                    child.AppendAnimation(animationStartTimeTag, animationOffsetTag);
                    //animationStartTimeTag = DateTime.MinValue;
                }
            }
            private void PutDown()
            {
                PutDown(l);
                PutDown(r);
                animationStartTimeTag = DateTime.MinValue;
                yOffsetTag = 0;
            }
            public double QueryYOffset()
            {
                if (animationStartTime == DateTime.MinValue)
                {
                    return yOffset;
                }
                else
                {
                    return yOffset + animationOffset * AnimationOffsetRatio((DateTime.Now - animationStartTime).TotalMilliseconds / animationDuration);
                }
            }
            public static int GetSize(TreapNode<DataType1> o) { return o == null ? 0 : o.size; }
            public void AppendAnimation(DateTime time, double offset)
            {
                {
                    double moved = AppendAnimation(ref animationStartTime, ref animationOffset, time, offset);
                    yOffset += moved;
                }
                {
                    double moved = AppendAnimation(ref animationStartTimeTag, ref animationOffsetTag, time, offset);
                    yOffsetTag += moved;
                }
            }
            public int GetPosition()
            {
                int position = GetSize(this.l);
                var o = this;
                for (; o.parent != null; o = o.parent)
                {
                    if (o.parent.r == o) position += GetSize(o.parent.l) + 1;
                }
                return position;
            }
            public static TreapNode<DataType1> Merge(TreapNode<DataType1> a, TreapNode<DataType1> b)
            {
                if (a == null || b == null) return a ?? b;
                if (a.priority > b.priority)
                {
                    a.PutDown();
                    if (a.r != null) a.r.parent = null;
                    a.r = Merge(a.r, b);
                    a.r.parent = a;
                    a.Maintain();
                    return a;
                }
                else
                {
                    b.PutDown();
                    if (b.l != null) b.l.parent = null;
                    b.l = Merge(a, b.l);
                    b.l.parent = b;
                    b.Maintain();
                    return b;
                }
            }
            public static void Split(TreapNode<DataType1> o, out TreapNode<DataType1> a, out TreapNode<DataType1> b, int position)
            {
                MyLogger.Assert(0 <= position && position <= GetSize(o));
                if (o == null) { a = b = null; return; }
                o.PutDown();
                if (position <= GetSize(o.l))
                {
                    b = o;
                    if (b.l != null) b.l.parent = null;
                    Split(b.l, out a, out b.l, position);
                    if (b.l != null) b.l.parent = b;
                    b.Maintain();
                }
                else
                {
                    a = o;
                    if (a.r != null) a.r.parent = null;
                    Split(a.r, out a.r, out b, position - GetSize(a.l) - 1);
                    if (a.r != null) a.r.parent = a;
                    a.Maintain();
                }
            }
        }
        TreapNode<DataType> root = null;
        public static double animationDuration = 1000;
        public double itemHeight = 50;
        private static double AnimationOffsetRatio(double timeRatio)
        {
            MyLogger.Assert(timeRatio >= 0);
            return Math.Min(1.0, timeRatio);
        }
        public int Count { get { return TreapNode<DataType>.GetSize(root); } }
        public TreapNode<DataType> Insert(DataType data, int position)
        {
            TreapNode<DataType>.Split(root, out TreapNode<DataType> a, out TreapNode<DataType> b, position);
            if (b != null) b.AppendAnimation(DateTime.Now, itemHeight);
            var o = new TreapNode<DataType>(data, position * itemHeight);
            root = TreapNode<DataType>.Merge(a, TreapNode<DataType>.Merge(o, b));
            return o;
        }
        public TreapNode<DataType> Delete(int position)
        {
            TreapNode<DataType>.Split(root, out TreapNode<DataType> b, out TreapNode<DataType> c, position + 1);
            TreapNode<DataType>.Split(b, out TreapNode<DataType> a, out b, position);
            if (c != null) c.AppendAnimation(DateTime.Now, -itemHeight);
            root = TreapNode<DataType>.Merge(a, c);
            return b;
        }
        public void Delete(TreapNode<DataType> o)
        {
            Delete(o.GetPosition());
        }
        public double Query(int position,Action<TreapNode<DataType>> callBack=null)
        {
            TreapNode<DataType>.Split(root, out TreapNode<DataType> b, out TreapNode<DataType> c, position + 1);
            TreapNode<DataType>.Split(b, out TreapNode<DataType> a, out b, position);
            double ans = b.QueryYOffset();
            callBack?.Invoke(b);
            root = TreapNode<DataType>.Merge(a, TreapNode<DataType>.Merge(b, c));
            return ans;
        }
    }
    public abstract class MyDisposable
    {
        public delegate void DisposedEventHandler();
        public event DisposedEventHandler Disposed;
        protected void OnDisposed() { Disposed?.Invoke(); }
    }
    public interface DataBindedView<DataType> where DataType: MyDisposable
    {
        void Reset(DataType data);
    }
    class BarsListPanel<GenericView,DataType>:MyContentView where DataType:MyDisposable where GenericView : Xamarin.Forms.View, DataBindedView<DataType>,new()
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
        bool isLayoutRunning = false, needRunAgain = false;
        private void UpdateLayout()
        {
            if (isLayoutRunning)
            {
                needRunAgain = true;
                return;
            }
            isLayoutRunning = true;
            index_RunAgain:;
            ALmain.AbortAnimation("animation");
            ALmain.HeightRequest = treap.itemHeight * (treap.Count + 1);
            LBend.TranslationY = treap.itemHeight * treap.Count;
            MyLogger.Log($"treap.Count: {treap.Count}");
            HashSet<DataType> remain = new HashSet<DataType>();
            foreach (var p in ChildrenInUse) remain.Add(p.Key);
            lock (treap)
            {
                if (treap.Count > 0)
                {
                    int l = UponIndex(), r = DownIndex();
                    for (int i = l; i <= r; i++)
                    {
                        treap.Query(i, new Action<Treap<DataType>.TreapNode<DataType>>((o) =>
                        {
                            if (ChildrenInUse.ContainsKey(o.data))
                            {
                                ChildrenInUse[o.data].TranslationY = o.QueryYOffset();
                                remain.Remove(o.data);
                            }
                            else
                            {
                                var c = GetGenericView();
                                c.TranslationY = o.QueryYOffset();
                                c.Reset(o.data);
                                ChildrenInUse[o.data] = c;
                            }
                        }));
                    }
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
            ALmain.Animate("animation", new Animation(new Action<double>((ratio) =>
            {
                if (treap.Count > 0)
                {
                    int l = UponIndex(), r = DownIndex();
                    for (int i = l; i <= r; i++)
                    {
                        treap.Query(i, new Action<Treap<DataType>.TreapNode<DataType>>((o) =>
                        {
                            if (ChildrenInUse.ContainsKey(o.data))
                            {
                                ChildrenInUse[o.data].TranslationY = o.QueryYOffset();
                            }
                        }));
                    }
                }
            })), 16, (uint)Treap<DataType>.animationDuration);
            if (needRunAgain)
            {
                needRunAgain = false;
                goto index_RunAgain;
            }
            isLayoutRunning = false;
        }
        private void RegisterEvents()
        {
            this.TreapLayoutChanged += () => { UpdateLayout(); };
            SVmain.Scrolled += (sender,args) => { UpdateLayout(); };
            SVmain.SizeChanged += (sender, args) => { UpdateLayout(); };
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
        }
    }
}
