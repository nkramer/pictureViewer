// based on http://blogs.msdn.com/b/llobo/archive/2006/09/06/scrolling-scrollviewer-on-mouse-drag-at-the-boundaries.aspx
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace pictureviewer
{
    // doesn't match Mouse.GetPos -- high dpi bug?
    public class MouseUtilities
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(ref Win32Point pt);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hwnd, ref Win32Point pt);

        public static Point GetMousePosition(Visual relativeTo)
        {
            Win32Point mouse = new Win32Point();
            GetCursorPos(ref mouse);

            System.Windows.Interop.HwndSource presentationSource =
                (System.Windows.Interop.HwndSource)PresentationSource.FromVisual(relativeTo);

            ScreenToClient(presentationSource.Handle, ref mouse);

            GeneralTransform transform = relativeTo.TransformToAncestor(presentationSource.RootVisual);

            Point offset = transform.Transform(new Point(0, 0));

            return new Point(mouse.X - offset.X, mouse.Y - offset.Y);
        }


    };


    public class DragDropScrollViewer : ScrollViewer
    {
        //public DragDropScrollViewer()
        //{
        
        //}

        //protected override void OnPreviewQueryContinueDrag(QueryContinueDragEventArgs args)
        //{
        //    base.OnPreviewQueryContinueDrag(args);

        //    if (args.Action == DragAction.Cancel || args.Action == DragAction.Drop)
        //    {
        //        CancelDrag();
        //    }
        //    else if (args.Action == DragAction.Continue)
        //    {
        //        Point p = Mouse.GetPosition(this);
        //        Debug.WriteLine("begin -- "+ p);
        //        if ((p.Y < s_dragMargin) || (p.Y > RenderSize.Height - s_dragMargin))
        //        {
        //            TickDragScroll(null, null);
        //            //if (_dragScrollTimer == null)
        //            //{
        //            //    _dragVelocity = s_dragInitialVelocity;
        //            //    _dragScrollTimer = new DispatcherTimer();
        //            //    _dragScrollTimer.Tick += TickDragScroll;
        //            //    _dragScrollTimer.Interval = new TimeSpan(0, 0, 0, 0, (int)s_dragInterval);
        //            //    _dragScrollTimer.Start();
        //            //}
        //        }
        //    }
        //}

        //private void TickDragScroll(object sender, EventArgs e)
        //{
        //    Debug.WriteLine("tick");
        //    bool isDone = true;

        //    if (this.IsLoaded)
        //    {
        //        Rect bounds = new Rect(RenderSize);
        //        Point p = MouseUtilities.GetMousePosition(this); //= Mouse.GetPosition(this);
        //        p.X = p.X / 1.2;
        //        p.Y = p.Y / 1.2;
        //        Debug.WriteLine("tick -- "+p+ " " + bounds);
        //        Debug.WriteLine(Mouse.GetPosition(this));
        //        if (bounds.Contains(p))
        //        {
        //            if (p.Y < s_dragMargin)
        //            {
        //                DragScroll(DragDirection.Up);
        //                isDone = false;
        //            }
        //            else if (p.Y > RenderSize.Height - s_dragMargin)
        //            {
        //                DragScroll(DragDirection.Down);
        //                isDone = false;
        //            }
        //        }
        //    }

        //    //if (isDone)
        //    //{
        //    //    CancelDrag();
        //    //}
        //}

        //private void CancelDrag()
        //{
        //    if (_dragScrollTimer != null)
        //    {
        //        _dragScrollTimer.Tick -= TickDragScroll;
        //        _dragScrollTimer.Stop();
        //        _dragScrollTimer = null;
        //    }
        //}

        //private enum DragDirection
        //{
        //    Down,
        //    Up
        //};

        //private void DragScroll(DragDirection direction)
        //{
        //    this.Background = Brushes.Red;
        //    bool isUp = (direction == DragDirection.Up);
        //    double offset = Math.Max(0.0, VerticalOffset + (isUp ? -(_dragVelocity * s_dragInterval) : (_dragVelocity * s_dragInterval)));
        //    ScrollToVerticalOffset(offset);
        //    _dragVelocity = Math.Min(s_dragMaxVelocity, _dragVelocity + (s_dragAcceleration * s_dragInterval));
        //}

        //private static readonly double s_dragInterval = 10; // milliseconds
        //private static readonly double s_dragAcceleration = 0.0005; // pixels per millisecond^2
        //private static readonly double s_dragMaxVelocity = 2.0; // pixels per millisecond
        //private static readonly double s_dragInitialVelocity = 0.05; // pixels per millisecond
        //private static double s_dragMargin = 40.0;
        //private DispatcherTimer _dragScrollTimer = null;
        //private double _dragVelocity;
    }
}
