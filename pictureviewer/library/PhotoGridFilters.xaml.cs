using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Pictureviewer.Core;
using Pictureviewer.Utilities;

namespace pictureviewer
{
    public partial class PhotoGridFilters : UserControl
    {
        private RootControl root;

        public PhotoGridFilters()
        {
            InitializeComponent();
        }

        public void Init(RootControl root, PhotoGrid photoGrid)
        {
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
        private void Tag_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var control = (sender as FrameworkElement);
            PhotoTag tag = control.DataContext as PhotoTag;
            if (e.ClickCount == 2) {
                root.AddFilter(root.AllOfTags, tag);
            } else {
                var data = new DragDropData(tag, DragDropOrigin.Tag, null);
                DataObject dragData = new DataObject(data);
                DragDrop.DoDragDrop(control, dragData, DragDropEffects.Copy);
            }
        }

        private void FilterTag_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var control = (sender as FrameworkElement);
            PhotoTag tag = control.DataContext as PhotoTag;
            FrameworkElement element = control;
            while (!(element is ItemsControl)) {
                element = VisualTreeHelper.GetParent(element) as FrameworkElement;
            }
            var collection = (ObservableCollection<PhotoTag>)((element as ItemsControl).ItemsSource);
            var data = new DragDropData(tag, DragDropOrigin.Filter, collection);
            DataObject dragData = new DataObject(data);

            DragDrop.DoDragDrop(control, dragData, DragDropEffects.Copy);
        }

        private void allOfItems_Drop(object sender, DragEventArgs e)
        {
            DropOnFilter(e, root.AllOfTags);
        }

        private void DropOnFilter(DragEventArgs e, ObservableCollection<PhotoTag> tags)
        {
            if (e.Data.GetDataPresent(typeof(DragDropData))) {
                var data = e.Data.GetData(typeof(DragDropData)) as DragDropData;
                var tag = data.Tag;
                root.AddFilter(tags, tag);
                if (data.DragDropOrigin == DragDropOrigin.Filter)
                    root.RemoveFilter(data.PreviousFilter, tag);
            }
        }

        private void anyOfItems_Drop(object sender, DragEventArgs e)
        {
            DropOnFilter(e, root.AnyOfTags);
        }

        private void excludeItems_Drop(object sender, DragEventArgs e)
        {
            DropOnFilter(e, root.ExcludeTags);
        }

        private void panel_Drop(object sender, DragEventArgs e)
        {
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

        private enum DragDropOrigin
        {
            Tag, Filter
        }

        private class DragDropData
        {
            public PhotoTag Tag;
            public DragDropOrigin DragDropOrigin;
            public ObservableCollection<PhotoTag> PreviousFilter;
            public DragDropData(PhotoTag tag, DragDropOrigin dragDropOrigin, ObservableCollection<PhotoTag> previousFilter)
            {
                this.Tag = tag;
                this.DragDropOrigin = dragDropOrigin;
                this.PreviousFilter = previousFilter;
            }
        }

        private void TagAdd_Click(object sender, RoutedEventArgs e)
        {
            var w = new QuestionWindow();
            w.Label = "Name of new tag";
            w.Result = "tag1";
            if (w.ShowDialog() == true) {
                var parent = (sender as FrameworkElement).DataContext as PhotoTag;
                Debug.Assert(parent != null);
                var t = new PhotoTag(w.Result, parent);
            }
        }

        private void TagRename_Click(object sender, RoutedEventArgs e)
        {
            var w = new QuestionWindow();
            w.Label = "New Name of tag";
            var tag = (sender as FrameworkElement).DataContext as PhotoTag;
            Debug.Assert(tag != null);
            w.Result = tag.Name;
            if (w.ShowDialog() == true) {
                tag.Name = w.Result;
            }
        }

        private void TagExcludeChildren_Click(object sender, RoutedEventArgs e)
        {
            var tag = (sender as FrameworkElement).DataContext as PhotoTag;
            foreach (var t in tag.Children) {
                root.AddFilter(root.ExcludeTags, t);
            }
        }

        private void Untag_Click(object sender, RoutedEventArgs e)
        {
            var tag = (sender as FrameworkElement).DataContext as PhotoTag;
            var photos = root.DisplaySet.Where(origin => origin.IsSelected);
            foreach (ImageOrigin photo in photos) {
                photo.RemoveTag(tag);
            }
        }

        private void TagDelete_Click(object sender, RoutedEventArgs e)
        {
            var tag = (sender as FrameworkElement).DataContext as PhotoTag;
            if (root.CompleteSet.FirstOrDefault(i => i.HasTag(tag)) != null) {
                MessageBox.Show("Can't delete; tag still in use");
            } else if (tag.Parent == null) {
                MessageBox.Show("Can't delete root tags");
            } else {
                tag.Parent = null;
            }
        }

        private void Tag_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            (sender as FrameworkElement).ContextMenu.IsOpen = true;
        }

        private void SelectedTag_Remove(object sender, RoutedEventArgs e)
        {
            var tag = (sender as FrameworkElement).DataContext as PhotoTag;
            var sel = root.DisplaySet.Where(i => i.IsSelected);
            foreach (ImageOrigin photo in sel) {
                if (photo.HasTag(tag))
                    photo.Tags.Remove(tag);
            }
        }

        private bool WillCreateCycle(PhotoTag tag, PhotoTag newParent)
        {
            if (tag == newParent) return true;
            if (newParent == null) return false;
            return WillCreateCycle(tag, newParent.Parent);
        }

        private void tree_Drop(object sender, DragEventArgs e)
        {
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
        private void TextBlock_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            PhotoTag tagInQuestion = (PhotoTag) ((FrameworkElement) sender).DataContext;
            (sender as FrameworkElement).ContextMenu.DataContext = tagInQuestion;
        }
    }
}
