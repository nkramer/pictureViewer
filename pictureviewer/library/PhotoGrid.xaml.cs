using Pictureviewer.Core;
using Pictureviewer.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace pictureviewer {
    public enum PhotoGridMode {
        Database,
        Designer,
    }

    public partial class PhotoGrid : UserControl, IScreen {
        public PhotoGridMode Mode = PhotoGridMode.Database;

        private RootControl root;
        private SelectableImageDisplay dragStart = null;
        private List<bool> dragPreviousSelection = null;
        //private SelectableImageDisplay dragLowest = null;
        //private SelectableImageDisplay dragHighest = null;

        private List<SelectableImageDisplay> displayList = new List<SelectableImageDisplay>();

        private SelectableImageDisplay focusedImageDisplay = null;

        private CommandHelper commands;
        private ContextMenu contextmenu = new ContextMenu();

        // hack -- should calc from grid size
        public int MaxPhotosToDisplay = 476; //200;//125;

        //  virtualization stuff
        private int firstDisplayed = 0;
        private Size lastSize = new Size(0, 0);
        private int numberVisible = 1;

        private ImageOrigin kbdSelectionStart = null;
        private List<bool> kbdPreviousSelection = null;

        internal PhotoGrid(RootControl root) {
            this.root = root;
            root.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(root_PropertyChanged);
            InitializeComponent();
            filters.Init(root, this);

            // do this before hooking up CommandHelper
            this.KeyDown += (o, e) => {
                if (e.Key == Key.Escape) {
                    bool undone = root.UndoFilter();
                    if (undone)
                        e.Handled = true;
                }
            };

            this.MouseWheel += new MouseWheelEventHandler(PhotoGrid_MouseWheel);
            Focusable = true;

            this.ContextMenu = contextmenu;
            this.commands = new CommandHelper(this);
            commands.contextmenu = contextmenu;
            CreateCommands();
            commands.MergeMenus(root.commands);

            panel.photogrid = this;
            panel.LayoutUpdated += new EventHandler(panel_LayoutUpdated);
            this.Loaded += (object sender, RoutedEventArgs e) => {
                // Defer until the container has a size & position
                if (root.TopScreen is PhotoGrid) {
                    // hack: prob better place to do this
                    root.loader.PrefetchPolicy = PrefetchPolicy.PhotoGrid;
                }
                // todo: calculate by screen size
                root.loader.ThumbnailsPerPage = 12 * 19; //10 * 17; // 9x12 = Number of thumbnails on old monitor
                // todo: A smarter cache policy would also cache the previous screen
                root.loader.FirstThumbnail = root.DisplaySet[0];
                root.loader.UpdateWorkItems();

                for (int i = firstDisplayed; i < MaxPhotosToDisplay; i++) {
                    var display = new SelectableImageDisplay();
                    panel.Children.Add(display);
                    SetUpImageDisplay(null, display);
                    SetUpImageDisplayHandlers(display);
                }
            };

            this.KeyUp += (object sender, KeyEventArgs e) => {
                if (e.Key == Key.LeftShift || e.Key == Key.RightShift) {
                    kbdSelectionStart = null;
                    kbdPreviousSelection = null;
                }
            };

            this.PreviewLostKeyboardFocus += new KeyboardFocusChangedEventHandler(PhotoGrid_PreviewLostKeyboardFocus);
        }

        private bool IsDesignMode { get { return Mode == PhotoGridMode.Designer; } }

        void PhotoGrid_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
            //   this.Focus();
        }

        void PhotoGrid_MouseWheel(object sender, MouseWheelEventArgs e) {
            int lines = -1 * e.Delta / System.Windows.Input.Mouse.MouseWheelDeltaForOneLine;
            int columnIncrement = lines * panel.Columns;
            int newFirstDisplayed = firstDisplayed + columnIncrement;
            newFirstDisplayed = Math.Min(newFirstDisplayed, RoundDownToRow(root.DisplaySet.Length - 1));
            newFirstDisplayed = Math.Max(0, newFirstDisplayed);
            SetViewport(newFirstDisplayed);
        }


        private void root_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == "" || e.PropertyName == "DisplaySet") {
                int focusedIndex = (root.FocusedImage == null) ? 0 : root.DisplaySet.ToList().IndexOf(root.FocusedImage);
                SetViewport(RoundDownToRow(focusedIndex));
            }
        }

        private void SetUpImageDisplay(ImageOrigin origin, SelectableImageDisplay display) {
            display.ImageDisplay.ImageInfo = null;
            display.ImageDisplay.ImageOrigin = null;
            display.ImageDisplay.ImageOrigin = origin;
            if (origin != null) {
                root.loader.BeginLoad(new LoadRequest(origin, 125 /* px */, 125 /* px */, ScalingBehavior.Thumbnail),
                    (info) => {
                        if (info.Origin == display.ImageDisplay.ImageOrigin) {
                            // guard against callbacks out of order
                            display.ImageDisplay.ImageInfo = info;
                        }
                    });
                display.ImageDisplay.ResetRotation(origin);
            }
            display.Height = 100;
            display.Width = 100;
            displayList.Add(display);
            if (origin == root.FocusedImage) {
                display.IsFocusedImage = true;
                this.focusedImageDisplay = display;
            } else {
                display.IsFocusedImage = false;
            }
        }

        private void UndoDragSelect() {
            for (int i = 0; i < displayList.Count; i++) {
                var imD = displayList[i].ImageDisplay;
                if (imD != null && imD.ImageOrigin != null)
                    imD.ImageOrigin.IsSelected = dragPreviousSelection[i];
            }
        }

        private void UndoKbdSelect() {
            Debug.Assert(kbdPreviousSelection != null);
            for (int i = 0; i < root.DisplaySet.Length; i++) {
                root.DisplaySet[i].IsSelected = kbdPreviousSelection[i];
            }
        }

        public class PhotoDragData {
            public ImageOrigin ImageOrigin;
            public bool SwapWithOrigin = false;
        }

        private void SetUpImageDisplayHandlers(SelectableImageDisplay display) {
            display.MouseDoubleClick += (sender2, args) => {
                ImageOrigin origin = display.ImageDisplay.ImageOrigin;
                if (Exited != null) {
                    Exited(this, new PhotoGridExitedEventArgs(origin));
                }
            };
            display.MouseDown += (sender2, args) => {
                if (IsDesignMode) {
                    if (display.ImageDisplay.ImageOrigin != null) {
                        var data = new PhotoDragData() { ImageOrigin = display.ImageDisplay.ImageOrigin };
                        DataObject dragData = new DataObject(data);
                        DragDrop.DoDragDrop(this, dragData, DragDropEffects.Copy);
                    }
                } else {
                    //CaptureMouse();
                    this.dragStart = display;
                    this.dragPreviousSelection = displayList.Select(d => d.ImageDisplay.ImageOrigin != null
                        && d.ImageDisplay.ImageOrigin.IsSelected).ToList();

                    SelectableImageDisplay oldFocus = this.focusedImageDisplay;
                    if (!(Keyboard.FocusedElement is SlideShow)) {
                        // hack -- MouseDown is called after MouseDoubleClick, don't want it to steal focus
                        this.Focus();
                    } else {
                        // similarly, don't consider dbl-click a drag start
                        this.dragStart = null;
                        this.dragPreviousSelection = null;
                    }
                    MoveFocus(display);
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) {
                        SelectRange(oldFocus, this.focusedImageDisplay);
                    } else if (Keyboard.Modifiers == ModifierKeys.Control) {
                        var i = focusedImageDisplay.ImageDisplay.ImageOrigin;
                        i.IsSelected = !i.IsSelected;
                    }
                }
            };
            display.MouseMove += (sender2, args) => {
                if (dragStart != null) {
                    UndoDragSelect();
                    if (dragStart != display) {
                        SelectRange(dragStart, display);
                    }
                }
            };
            display.MouseUp += (sender2, args) => {
                dragStart = null;
                dragPreviousSelection = null;
                ReleaseMouseCapture();
            };
            display.LostMouseCapture += (sender2, args) => {
                dragStart = null;
                dragPreviousSelection = null;
            };
        }

        private void SelectRange(SelectableImageDisplay oldFocus, SelectableImageDisplay newFocus) {
            int old = displayList.IndexOf(oldFocus);
            int nw = displayList.IndexOf(newFocus);
            if (old != -1 && nw != -1) {
                var displays = displayList.GetRange(Math.Min(old, nw), Math.Abs(old - nw) + 1);
                bool newValue = ((Keyboard.Modifiers & ModifierKeys.Control) != 0) ? false : true;
                // !displays.All(d => d.ImageDisplay.ImageOrigin.IsSelected);
                foreach (var d in displays) {
                    var i = d.ImageDisplay.ImageOrigin;
                    if (i != null)
                        i.IsSelected = newValue;
                }
            }
        }

        // used by kbd selection
        private void SelectRange(ImageOrigin oldFocus, ImageOrigin newFocus) {
            int old = ImageOrigin.GetIndex(root.DisplaySet, oldFocus);
            int nw = ImageOrigin.GetIndex(root.DisplaySet, newFocus);
            IEnumerable<ImageOrigin> origins = root.DisplaySet
                .Where((origin, index) => index >= Math.Min(old, nw) && index <= Math.Max(old, nw));
            bool newValue = ((Keyboard.Modifiers & ModifierKeys.Control) != 0) ? false : true;
            foreach (var i in origins) {
                i.IsSelected = newValue;
            }
        }

        private void panel_LayoutUpdated(object sender, EventArgs e) {
            var size = new Size(panel.ActualWidth, panel.ActualHeight);
            if (size != lastSize) {
                this.numberVisible = panel.numberVisible;
                this.lastSize = size;
                SetViewport(this.firstDisplayed);
            }
        }

        private void SetViewport(int firstDisplayed) {
            Debug.Assert(numberVisible >= 0);
            Debug.Assert(firstDisplayed >= 0);
            Debug.Assert(firstDisplayed < root.DisplaySet.Length || (root.DisplaySet.Length == 0 && firstDisplayed == 0));
            if (panel.Columns > 0)
                Debug.Assert(firstDisplayed % panel.Columns == 0); // not true if resized

            int oldfirstDisplayed = this.firstDisplayed;

            this.firstDisplayed = firstDisplayed;
            //this.numberVisible = numberVisible;
            root.loader.FirstThumbnail = root.DisplaySet.Length > 0 ? root.DisplaySet[firstDisplayed] : null;
            root.loader.UpdateWorkItems();

            displayList.Clear();
            for (int i = 0; i < numberVisible; i++) {
                int index = i + firstDisplayed;
                var imageDisplay = panel.Children[i] as SelectableImageDisplay;
                // bug: if window is too big, we run out of panel.Children
                if (i < root.DisplaySet.Length - firstDisplayed)
                    SetUpImageDisplay(root.DisplaySet[index], imageDisplay);
                else {
                    SetUpImageDisplay(null, imageDisplay);
                }
            }

            scrollbar.Minimum = 0;
            //scrollbar.Maximum = root.DisplaySet.Length - numberVisible;
            scrollbar.Maximum = RoundDownToRow(root.DisplaySet.Length - numberVisible + panel.Columns);
            scrollbar.ViewportSize = numberVisible;
            scrollbar.SmallChange = panel.Columns;
            scrollbar.LargeChange = numberVisible;
            scrollbar.Value = firstDisplayed;

            // is focus still in view?
            var photosInView = root.DisplaySet.Skip(firstDisplayed - 1).Take(numberVisible);
            if (photosInView.FirstOrDefault() != null && firstDisplayed != oldfirstDisplayed
                  && !photosInView.Contains(root.FocusedImage)) {
                if (firstDisplayed > oldfirstDisplayed)
                    MoveFocus(panel.Children[0] as SelectableImageDisplay);
                else
                    MoveFocus(panel.Children[numberVisible - 1] as SelectableImageDisplay);
            }
        }

        public void MoveFocus(ImageOrigin origin) {
            int newIndex = root.DisplaySet.ToList().IndexOf(origin);
            Debug.Assert(newIndex != -1);
            ScrollIntoView(newIndex);
            SelectableImageDisplay d = displayList.First(display => display.ImageDisplay.ImageOrigin == origin);
            MoveFocus(d);
        }

        private void MoveFocus(SelectableImageDisplay newFocus) {
            if (focusedImageDisplay != null) {
                focusedImageDisplay.IsFocusedImage = false;
            }
            focusedImageDisplay = newFocus;
            root.FocusedImage = focusedImageDisplay.ImageDisplay.ImageOrigin;
            focusedImageDisplay.IsFocusedImage = true;
        }

        private void scrollbar_Scroll(object sender, ScrollEventArgs e) {
            int newFirstDisplayed = (int)Math.Round(scrollbar.Value);
            newFirstDisplayed = RoundDownToRow(newFirstDisplayed);
            SetViewport(newFirstDisplayed);
        }

        private int RoundDownToRow(int newFirstDisplayed) {
            if (panel.Columns == 0)
                return newFirstDisplayed;
            else
                return (newFirstDisplayed / panel.Columns) * panel.Columns;
        }

        // keyboard
        private void MoveColumn(int increment, bool select) {
            if (select && kbdSelectionStart == null) {
                this.kbdSelectionStart = focusedImageDisplay.ImageDisplay.ImageOrigin;
                this.kbdPreviousSelection = root.DisplaySet.Select(o => o.IsSelected).ToList();
            } else if (!select) {
                this.kbdSelectionStart = null;
                this.kbdPreviousSelection = null;
            }
            if (select) {
                Debug.Assert(kbdSelectionStart != null);
                Debug.Assert(kbdPreviousSelection != null);
            }

            // index is index into the root.DisplaySet
            int index = displayList.IndexOf(focusedImageDisplay) + firstDisplayed;
            int newIndex = index + increment;

            // stop at end of collection
            if (newIndex < 0) newIndex = 0;
            if (newIndex >= root.DisplaySet.Length) newIndex = root.DisplaySet.Length - 1;

            ImageOrigin newFocusOrigin = root.DisplaySet[newIndex];

            ScrollIntoView(newIndex);

            MoveFocus(displayList.First(d => d.ImageDisplay.ImageOrigin == newFocusOrigin));
            if (select) {
                UndoKbdSelect();
                SelectRange(kbdSelectionStart, newFocusOrigin);
            }
        }

        private void ScrollIntoView(int newIndex) {
            // do we need to scroll?
            int scrollUp = Math.Min(0, newIndex - firstDisplayed); // 0 or negative
            int scrollDown = Math.Max(0, newIndex - (firstDisplayed + numberVisible - 1));
            int scroll = scrollDown + scrollUp;
            // those are indexes not # lines

            // Round up to nearest row
            // -10 % 6 == -4, -10 / 6 == -1
            Debug.Assert(scroll == (scroll / panel.Columns) * panel.Columns + scroll % panel.Columns);
            if (scroll % panel.Columns != 0)
                scroll = (scroll / panel.Columns + Math.Sign(scroll % panel.Columns)) * panel.Columns;

            int oldFirstDisplayed = firstDisplayed;
            int newFirstDisplayed = firstDisplayed + scroll;
            SetViewport(newFirstDisplayed);
        }

        private void MoveRow(int increment, bool select) {
            int columnIncrement = increment * panel.Columns;
            MoveColumn(columnIncrement, select);
        }

        private void CreateCommands() {
            Command command;

            //#if WPF
            //            command = new Command();
            //            command.Text = "Exit grid";
            //            command.HasMenuItem = false;
            //            command.Key = Key.Escape;
            //            command.Execute += delegate() {
            //                if (Exited != null) {
            //                    Exited(this, new PhotoGridExitedEventArgs(focusedImageDisplay.ImageDisplay.ImageOrigin));
            //                }
            //            };
            //            commands.AddCommand(command);
            //#endif

            command = new Command();
            command.Key = Key.Enter;
            command.HasMenuItem = false;
            command.Execute += delegate () {
                if (Exited != null) {
                    Exited(this, new PhotoGridExitedEventArgs(focusedImageDisplay.ImageDisplay.ImageOrigin));
                }
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.Space;
            command.HasMenuItem = false;
            command.Execute += delegate () {
                var origin = focusedImageDisplay.ImageDisplay.ImageOrigin;
                origin.IsSelected = !origin.IsSelected;
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.Right;
            command.HasMenuItem = false;
            command.WithOrWithoutShift = true;
            command.Execute += delegate () {
                //for (int i=0;i<10;i++) 
                MoveColumn(1, CommandHelper.IsShiftPressed);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.Left;
            command.WithOrWithoutShift = true;
            command.HasMenuItem = false;
            command.Execute += delegate () {
                MoveColumn(-1, CommandHelper.IsShiftPressed);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.Down;
            command.WithOrWithoutShift = true;
            command.HasMenuItem = false;
            command.Execute += delegate () {
                MoveRow(1, CommandHelper.IsShiftPressed);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.Up;
            command.WithOrWithoutShift = true;
            command.HasMenuItem = false;
            command.Execute += delegate () {
                MoveRow(-1, CommandHelper.IsShiftPressed);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.PageDown;
            command.HasMenuItem = false;
            command.WithOrWithoutShift = true;
            command.Execute += delegate () {
                if (firstDisplayed + numberVisible < root.DisplaySet.Length) {
                    int newFirstDisplayed = firstDisplayed + numberVisible;
                    SetViewport(newFirstDisplayed);
                }
                SelectableImageDisplay last = displayList.LastOrDefault(d => d.ImageDisplay.ImageOrigin != null);
                MoveFocus(last);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.PageUp;
            command.HasMenuItem = false;
            command.WithOrWithoutShift = true;
            command.Execute += delegate () {
                int newFirstDisplayed = Math.Max(0, firstDisplayed - numberVisible);
                SetViewport(newFirstDisplayed);
                MoveFocus(displayList[0]);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.Home;
            command.HasMenuItem = false;
            command.WithOrWithoutShift = true;
            command.Execute += delegate () {
                int newFirstDisplayed = 0;
                SetViewport(newFirstDisplayed);
                MoveFocus(displayList[0]);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.End;
            command.HasMenuItem = false;
            command.WithOrWithoutShift = true;
            command.Execute += delegate () {
                MoveColumn(500000, CommandHelper.IsShiftPressed); // large but no int overflows
                //int lastRow = (root.DisplaySet.Length - 1) / panel.Columns;
                ////int fd = root.DisplaySet.Length - lastRow * panel.Columns;
                //int fd = lastRow * panel.Columns - numberVisible + panel.Columns;
                //int newFirstDisplayed = Math.Max(0, fd);
                //SetViewport(newFirstDisplayed);
                //SelectableImageDisplay last = displayList.LastOrDefault(d => d.ImageDisplay.ImageOrigin != null);
                //MoveFocus(last);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.R;
            command.Text = "Rotate selected photos";
            command.Execute += delegate () {
                var list = new System.Collections.Specialized.StringCollection();
                foreach (var im in root.DisplaySet.Where(i => i.IsSelected)) {
                    im.Rotation += 90;
                }
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.S;
            command.ModifierKeys = ModifierKeys.Shift;
            command.Text = "Clear selection/select all";
            command.Execute += delegate () {
                bool oldVal = root.DisplaySet.Any(i => i.IsSelected);
                foreach (var i in root.DisplaySet) {
                    i.IsSelected = !oldVal;
                }
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.I;
            command.Text = "Invert selection";
            command.Execute += delegate () {
                foreach (var i in root.DisplaySet) {
                    i.IsSelected = !i.IsSelected;
                }
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.C;
            command.Text = "Copy selected photos";
            command.Execute += delegate () {
                var list = new System.Collections.Specialized.StringCollection();
                var files = root.DisplaySet.Where(i => i.IsSelected).Select(i => i.SourcePath).ToArray();
                list.AddRange(files);
                if (list.Count > 0)
                    Clipboard.SetFileDropList(list);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.D1;
            command.Text = "1-star photos";
            command.Execute += delegate () {
                PhotoTag tag = PhotoTag.FindOrMake(PhotoTag.GetRatingString(1), root.Tags);
                root.AddFilter(root.AllOfTags, tag);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.D2;
            command.Text = "2-star photos";
            command.Execute += delegate () {
                PhotoTag tag = PhotoTag.FindOrMake(PhotoTag.GetRatingString(2), root.Tags);
                root.AddFilter(root.AllOfTags, tag);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.D3;
            command.Text = "3-star photos";
            command.Execute += delegate () {
                PhotoTag tag = PhotoTag.FindOrMake(PhotoTag.GetRatingString(3), root.Tags);
                root.AddFilter(root.AllOfTags, tag);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.D4;
            command.Text = "4-star photos";
            command.Execute += delegate () {
                PhotoTag tag = PhotoTag.FindOrMake(PhotoTag.GetRatingString(4), root.Tags);
                root.AddFilter(root.AllOfTags, tag);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Key = Key.D5;
            command.Text = "5-star photos";
            command.Execute += delegate () {
                PhotoTag tag = PhotoTag.FindOrMake(PhotoTag.GetRatingString(5), root.Tags);
                root.AddFilter(root.AllOfTags, tag);
            };
            commands.AddCommand(command);
        }

        public event EventHandler<PhotoGridExitedEventArgs> Exited;

        void IScreen.Activate(ImageOrigin focus) {
            root.loader.PrefetchPolicy = PrefetchPolicy.PhotoGrid;
            if (focus != null)
                this.MoveFocus(focus);
        }

        void IScreen.Deactivate() {

        }
    }



    public class PhotoGridExitedEventArgs : EventArgs {
        public PhotoGridExitedEventArgs(ImageOrigin clickedImageOrigin) {
            this.ClickedImageOrigin = clickedImageOrigin;
        }

        public ImageOrigin ClickedImageOrigin = null;
    }

    //public class Foo : PhotoGrid
    //{
    //    public Foo(RootControl root) : base(root) {
    //    }
    //}
}
