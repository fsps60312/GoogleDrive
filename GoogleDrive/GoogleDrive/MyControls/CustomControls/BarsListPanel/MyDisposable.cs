using System;

namespace GoogleDrive.MyControls.BarsListPanel
{
    public abstract class MyDisposable
    {
        public delegate void MyDisposableEventHandler();
        public event MyDisposableEventHandler Disposed;
        public Func<System.Threading.Tasks.Task>Disposing = null;
        public void UnregisterDisposingEvents() { Disposing = null; }
        protected async System.Threading.Tasks.Task OnDisposed()
        {
            if (Disposing != null) await Disposing();
            Disposed?.Invoke();
            Disposing = null; Disposed = null;
        }
    }
}
