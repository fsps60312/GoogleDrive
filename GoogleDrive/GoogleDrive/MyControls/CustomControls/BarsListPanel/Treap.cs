using System;

namespace GoogleDrive.MyControls.BarsListPanel
{
    public partial class Treap<DataType>
    {
        //public Treap()
        //{
        //    Insert(, 0);
        //}
        public delegate void TreapDataEventHandler(DataType data);
        public event TreapDataEventHandler DataInserted, DataRemoved;
        private void OnDataInserted(DataType data) { DataInserted?.Invoke(data); }
        private void OnDataRemoved(DataType data) { DataRemoved?.Invoke(data); }
        TreapNode root = new TreapNode(default(DataType), 0);
        public static double animationDuration = 500;
        public double itemHeight = 50;
        private static double AnimationOffsetRatio(double timeRatio)
        {
            MyLogger.Assert(timeRatio >= 0);
            return Math.Min(1.0, timeRatio);
        }
        public int Count { get { return TreapNode.GetSize(root) - 1; } }
        public void DisposeAll()
        {
            for (int i = Count - 1; i >= 0; i--)
            {
                Query(i, new Action<TreapNode>(async (o) =>
                 {
                     await (o.data as MyDisposable).OnDisposed();
                 }));
            }
        }
        public TreapNode Insert(DataType data, int position)
        {
            var height = QueryFinal(position);// (position <= Count ?  : position * itemHeight);
            try
            {
                lock (root)
                {
                    TreapNode.Split(root, out TreapNode a, out TreapNode b, position);
                    if (b != null) b.AppendAnimation(DateTime.Now, itemHeight);
                    var o = new TreapNode(data, height);
                    root = TreapNode.Merge(a, TreapNode.Merge(o, b));
                    return o;
                }
            }
            finally
            {
                OnDataInserted(data);
            }
        }
        public int QueryLowerBound(double targetY)
        {
            lock (root)
            {
                return (root == null ? 0 : root.QueryLowerBound(targetY));
            }
        }
        private double QueryFinal(int position)
        {
            double ans;
            TreapNode b;
            lock (root)
            {
                TreapNode.Split(root, out b, out TreapNode c, position + 1);
                TreapNode.Split(b, out TreapNode a, out b, position);
                ans = b.QueryFinalYOffset();
                root = TreapNode.Merge(a, TreapNode.Merge(b, c));
            }
            return ans;
        }
        public TreapNode Delete(int position)
        {
            lock (root)
            {
                TreapNode.Split(root, out TreapNode b, out TreapNode c, position + 1);
                TreapNode.Split(b, out TreapNode a, out b, position);
                if (c != null) c.AppendAnimation(DateTime.Now, -itemHeight);
                root = TreapNode.Merge(a, c);
                OnDataRemoved(b.data);
                return b;
            }
        }
        public void Delete(TreapNode o)
        {
            Delete(o.GetPosition());
        }
        public double Query(int position, Action<TreapNode> callBack = null)
        {
            double ans;
            TreapNode b;
            lock (root)
            {
                TreapNode.Split(root, out b, out TreapNode c, position + 1);
                TreapNode.Split(b, out TreapNode a, out b, position);
                ans = b.QueryYOffset();
                root = TreapNode.Merge(a, TreapNode.Merge(b, c));
            }
            callBack?.Invoke(b);
            return ans;
        }
        public void ChangeHeight(TreapNode o,double difference)
        {
            lock(root)
            {
                TreapNode.Split(root, out TreapNode a, out TreapNode b, o.GetPosition() + 1);
                if (b != null) b.AppendAnimation(DateTime.Now, difference);
                root = TreapNode.Merge(a, b);
            }
        }
    }
}
