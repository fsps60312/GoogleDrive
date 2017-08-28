using System;

namespace GoogleDrive.MyControls.BarsListPanel
{
    public partial class Treap<DataType>
    {
        //public Treap()
        //{
        //    Insert(, 0);
        //}
        TreapNode root = new TreapNode(default(DataType), 0);
        public static double animationDuration = 500;
        public double itemHeight = 50;
        private static double AnimationOffsetRatio(double timeRatio)
        {
            MyLogger.Assert(timeRatio >= 0);
            return Math.Min(1.0, timeRatio);
        }
        public int Count { get { return TreapNode.GetSize(root) - 1; } }
        public TreapNode Insert(DataType data, int position)
        {
            lock (root)
            {
                TreapNode.Split(root, out TreapNode a, out TreapNode b, position);
                if (b != null) b.AppendAnimation(DateTime.Now, itemHeight);
                var o = new TreapNode(data, position * itemHeight);
                root = TreapNode.Merge(a, TreapNode.Merge(o, b));
                return o;
            }
        }
        public TreapNode Delete(int position)
        {
            lock (root)
            {
                TreapNode.Split(root, out TreapNode b, out TreapNode c, position + 1);
                TreapNode.Split(b, out TreapNode a, out b, position);
                if (c != null) c.AppendAnimation(DateTime.Now, -itemHeight);
                root = TreapNode.Merge(a, c);
                return b;
            }
        }
        public void Delete(TreapNode o)
        {
            Delete(o.GetPosition());
        }
        public double Query(int position, Action<TreapNode> callBack = null)
        {
            lock (root)
            {
                TreapNode.Split(root, out TreapNode b, out TreapNode c, position + 1);
                TreapNode.Split(b, out TreapNode a, out b, position);
                double ans = b.QueryYOffset();
                callBack?.Invoke(b);
                root = TreapNode.Merge(a, TreapNode.Merge(b, c));
                return ans;
            }
        }
    }
}
