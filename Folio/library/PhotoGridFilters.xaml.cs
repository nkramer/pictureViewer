using Folio.Core;
using Folio.Shell;
using Folio.Utilities;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Folio.Library {
    public partial class PhotoGridFilters : UserControl {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private RootControl root;
        private Popup? dragFeedbackPopup = null;
        private TextBlock? dragFeedbackTextBlock = null;

        public PhotoGridFilters() {
            InitializeComponent();

            // Hook up drag feedback event handlers
            this.GiveFeedback += new GiveFeedbackEventHandler(PhotoGridFilters_GiveFeedback);
            this.QueryContinueDrag += new QueryContinueDragEventHandler(PhotoGridFilters_QueryContinueDrag);
        }

        public void Init(RootControl root, PhotoGrid photoGrid) {
            this.root = root;
            photoGrid.panel.AllowDrop = true;
            photoGrid.panel.Drop += panel_Drop;

            this.tree.Items.Clear();
            this.tree.ItemsSource = root.Tags;
            this.allOfItems.ItemsSource = root.AllOfTags;
            this.anyOfItems.ItemsSource = root.AnyOfTags;
            this.excludeItems.ItemsSource = root.ExcludeTags;
            this.selPhotoTags.ItemsSource = root.SelectedPhotoTags;
        }

        // http://wpftutorial.net/DragAndDrop.html
        private void Tag_MouseDown(object sender, MouseButtonEventArgs e) {
            var control = (sender as FrameworkElement);
            PhotoTag tag = control.DataContext as PhotoTag;
            if (e.ClickCount == 2) {
                root.AddFilter(root.AllOfTags, tag);
            } else {
                var data = new DragDropData(tag, DragDropOrigin.Tag, null);
                DataObject dragData = new DataObject(data);

                // Create drag feedback before starting drag
                CreateDragFeedback(tag.Name);

                DragDrop.DoDragDrop(control, dragData, DragDropEffects.Copy);

                // Clean up drag feedback after drag completes
                HideDragFeedback();
            }
        }

        private void FilterTag_MouseDown(object sender, MouseButtonEventArgs e) {
            var control = (sender as FrameworkElement);
            PhotoTag tag = control.DataContext as PhotoTag;
            FrameworkElement element = control;
            while (!(element is ItemsControl)) {
                element = VisualTreeHelper.GetParent(element) as FrameworkElement;
            }
            var collection = (ObservableCollection<PhotoTag>)((element as ItemsControl).ItemsSource);
            var data = new DragDropData(tag, DragDropOrigin.Filter, collection);
            DataObject dragData = new DataObject(data);

            // Create drag feedback before starting drag
            CreateDragFeedback(tag.Name);

            DragDrop.DoDragDrop(control, dragData, DragDropEffects.Copy);

            // Clean up drag feedback after drag completes
            HideDragFeedback();
        }

        private void allOfItems_Drop(object sender, DragEventArgs e) {
            DropOnFilter(e, root.AllOfTags);
        }

        private void DropOnFilter(DragEventArgs e, ObservableCollection<PhotoTag> tags) {
            if (e.Data.GetDataPresent(typeof(DragDropData))) {
                var data = e.Data.GetData(typeof(DragDropData)) as DragDropData;
                var tag = data.Tag;
                root.AddFilter(tags, tag);
                if (data.DragDropOrigin == DragDropOrigin.Filter)
                    root.RemoveFilter(data.PreviousFilter, tag);
            }
        }

        private void anyOfItems_Drop(object sender, DragEventArgs e) {
            DropOnFilter(e, root.AnyOfTags);
        }

        private void excludeItems_Drop(object sender, DragEventArgs e) {
            DropOnFilter(e, root.ExcludeTags);
        }

        private void panel_Drop(object sender, DragEventArgs e) {
            var photos = root.DisplaySet.Where(origin => origin.IsSelected);
            if (e.Data.GetDataPresent(typeof(DragDropData))) {
                var data = e.Data.GetData(typeof(DragDropData)) as DragDropData;
                var tag = data.Tag;
                if (data.DragDropOrigin == DragDropOrigin.Filter) {
                    root.RemoveFilter(data.PreviousFilter, tag);
                } else {
                    foreach (ImageOrigin photo in photos) {
                        photo.AddTag(tag);
                    }
                }
            }
        }

        void PhotoGridFilters_GiveFeedback(object sender, GiveFeedbackEventArgs e) {
            // Use custom cursor feedback
            e.UseDefaultCursors = false;
            e.Handled = true;

            // Update popup position to follow cursor
            UpdateDragFeedbackPosition();
        }

        void PhotoGridFilters_QueryContinueDrag(object sender, QueryContinueDragEventArgs e) {
            // Update popup position during drag
            UpdateDragFeedbackPosition();
        }

        private void CreateDragFeedback(string tagName) {
            // Create the text block for drag feedback
            dragFeedbackTextBlock = new TextBlock();
            dragFeedbackTextBlock.Text = tagName;
            dragFeedbackTextBlock.Foreground = new SolidColorBrush(Colors.White);
            dragFeedbackTextBlock.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"));
            dragFeedbackTextBlock.Padding = new Thickness(8, 4, 8, 4);
            dragFeedbackTextBlock.FontSize = 14;

            // Create a border for additional styling (optional)
            var border = new Border();
            border.Child = dragFeedbackTextBlock;
            border.BorderBrush = new SolidColorBrush(Colors.Gray);
            border.BorderThickness = new Thickness(1);
            border.CornerRadius = new CornerRadius(4);

            // Create popup with absolute positioning
            dragFeedbackPopup = new Popup();
            dragFeedbackPopup.Child = border;
            dragFeedbackPopup.AllowsTransparency = true;
            dragFeedbackPopup.IsHitTestVisible = false;
            dragFeedbackPopup.Placement = PlacementMode.Absolute;

            // Position and show popup
            UpdateDragFeedbackPosition();
            dragFeedbackPopup.IsOpen = true;
        }

        private void UpdateDragFeedbackPosition() {
            if (dragFeedbackPopup != null && dragFeedbackPopup.IsOpen) {
                // Get cursor position in screen coordinates (physical pixels)
                POINT cursorPos;
                if (GetCursorPos(out cursorPos)) {
                    // Convert screen coordinates to device-independent pixels (DIPs)
                    // WPF Popup uses DIPs, not physical pixels
                    var window = Window.GetWindow(this);
                    if (window != null) {
                        var source = PresentationSource.FromVisual(window);
                        if (source != null) {
                            double dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                            double dpiScaleY = source.CompositionTarget.TransformToDevice.M22;

                            // Convert to DIPs and add offset
                            dragFeedbackPopup.HorizontalOffset = (cursorPos.X / dpiScaleX) + 10;
                            dragFeedbackPopup.VerticalOffset = (cursorPos.Y / dpiScaleY) + 10;
                            return;
                        }
                    }

                    // Fallback if we can't get DPI scaling (assume 96 DPI = 1.0 scale)
                    dragFeedbackPopup.HorizontalOffset = cursorPos.X + 10;
                    dragFeedbackPopup.VerticalOffset = cursorPos.Y + 10;
                }
            }
        }

        private void HideDragFeedback() {
            if (dragFeedbackPopup != null) {
                dragFeedbackPopup.IsOpen = false;
                dragFeedbackPopup = null;
            }
            dragFeedbackTextBlock = null;
        }

        private enum DragDropOrigin {
            Tag, Filter
        }

        private class DragDropData {
            public PhotoTag Tag;
            public DragDropOrigin DragDropOrigin;
            public ObservableCollection<PhotoTag>? PreviousFilter;
            public DragDropData(PhotoTag tag, DragDropOrigin dragDropOrigin, ObservableCollection<PhotoTag>? previousFilter) {
                this.Tag = tag;
                this.DragDropOrigin = dragDropOrigin;
                this.PreviousFilter = previousFilter;
            }
        }

        private void TagAdd_Click(object sender, RoutedEventArgs e) {
            var w = new QuestionWindow();
            w.DialogTitle = "Name of new tag";
            w.Result = "tag1";
            if (w.ShowDialog() == true) {
                var parent = (sender as FrameworkElement).DataContext as PhotoTag;
                Debug.Assert(parent != null);
                var t = new PhotoTag(w.Result, parent);
            }
        }

        private void TagRename_Click(object sender, RoutedEventArgs e) {
            var w = new QuestionWindow();
            w.DialogTitle = "New Name of tag";
            var tag = (sender as FrameworkElement).DataContext as PhotoTag;
            Debug.Assert(tag != null);
            w.Result = tag.Name;
            if (w.ShowDialog() == true) {
                tag.Name = w.Result;
            }
        }

        private void TagExcludeChildren_Click(object sender, RoutedEventArgs e) {
            var tag = (sender as FrameworkElement).DataContext as PhotoTag;
            foreach (var t in tag.Children) {
                root.AddFilter(root.ExcludeTags, t);
            }
        }

        private void Untag_Click(object sender, RoutedEventArgs e) {
            var tag = (sender as FrameworkElement).DataContext as PhotoTag;
            var photos = root.DisplaySet.Where(origin => origin.IsSelected);
            foreach (ImageOrigin photo in photos) {
                photo.RemoveTag(tag);
            }
        }

        private void TagDelete_Click(object sender, RoutedEventArgs e) {
            var tag = (sender as FrameworkElement).DataContext as PhotoTag;
            if (root.CompleteSet.FirstOrDefault(i => i.HasTag(tag)) != null) {
                ThemedMessageBox.Show("Can't delete; tag still in use");
            } else if (tag.Parent == null) {
                ThemedMessageBox.Show("Can't delete root tags");
            } else {
                tag.Parent = null;
            }
        }

        private void Tag_MouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            (sender as FrameworkElement).ContextMenu.IsOpen = true;
        }

        private void SelectedTag_Remove(object sender, RoutedEventArgs e) {
            var tag = (sender as FrameworkElement).DataContext as PhotoTag;
            var sel = root.DisplaySet.Where(i => i.IsSelected);
            foreach (ImageOrigin photo in sel) {
                if (photo.HasTag(tag))
                    photo.Tags.Remove(tag);
            }
        }

        private bool WillCreateCycle(PhotoTag tag, PhotoTag newParent) {
            if (tag == newParent) return true;
            if (newParent == null) return false;
            return WillCreateCycle(tag, newParent.Parent);
        }

        private void tree_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(typeof(DragDropData))) {
                var data = e.Data.GetData(typeof(DragDropData)) as DragDropData;
                var tag = data.Tag;
                if (data.DragDropOrigin == DragDropOrigin.Filter) {
                    root.RemoveFilter(data.PreviousFilter, tag);
                } else {
                    PhotoTag dropTarget = (sender as FrameworkElement).DataContext as PhotoTag;
                    if (!WillCreateCycle(tag, dropTarget)) {
                        tag.Parent = dropTarget;
                    }
                }
            }
        }

        // hack -- menuitem has no datacontext
        private void TextBlock_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
            PhotoTag tagInQuestion = (PhotoTag)((FrameworkElement)sender).DataContext;
            (sender as FrameworkElement).ContextMenu.DataContext = tagInQuestion;
        }
    }
}
