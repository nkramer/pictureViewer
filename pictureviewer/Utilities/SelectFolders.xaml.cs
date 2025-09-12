using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Pictureviewer.Core;
using pictureviewer;

namespace Pictureviewer.Utilities
{
    public partial class SelectFolders : Window
    {
        private Style itemStyle = null;
        private FileListSource fileListSource;

        public SelectFolders(FileListSource fileListSource)
        {
            InitializeComponent();

            this.fileListSource = fileListSource;

            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.ResizeMode = ResizeMode.NoResize;
            SourceDirectory = null;

            itemStyle = (Style)Resources["itemStyle"];
            InitializeTreeView(tree);

            InitializeTreeView(targetTree);
            targetTree.Items.Add(CreateItem("c:\\", "test"));
            tree.Focus();
        }

        private string sourceDirectory;
        private string automaticTargetDirectory = null;
        private string manualTargetDirectory = null;

        public string SourceDirectory
        {
            get { return sourceDirectory; }
            set
            {
                if (shutdown) {
                    throw new Exception("can't reuse this dialog");
                }

                sourceDirectory = value;
                if (sourceDirectory == null) {
                    // pick last dir w/ date
                    //sourceDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                    sourceDirectory = RootControl.picDir;
                    string[] dirs = Directory.GetDirectories(sourceDirectory);
                    foreach (var dir in dirs) {
                        if (char.IsDigit(Path.GetFileName(dir)[0]))
                            sourceDirectory = dir;
                    }
                }

                SelectDirectory(sourceDirectory, tree);
                automaticTargetDirectory = ChooseTargetDirectory(SourceDirectory);
                automaticTargetLabel.Text = automaticTargetDirectory;
                if (automaticTargetDirectory == null) {
                    targetManual.IsChecked = true;
                }
            }
        }

        public string TargetDirectory
        {
            get
            {
                if (targetAutomatic.IsChecked == true) {
                    return automaticTargetDirectory;
                } else {
                    return manualTargetDirectory;
                }
            }
        }

        private static string NormalizeMonth(string month)
        {
            if (month.StartsWith("0"))
                return month.Substring(1);
            else
                return month;
        }

        private string ChooseTargetDirectory(string sourceDirectory)
        {
            string directoryName = Path.GetFileName(sourceDirectory);
            //string directoryName = "2008-07-02 lake 22";

            var datedRegex = new Regex(@"^(?'year'\d\d\d\d)-(?'month'\d+)-(?'day'\d+)(.*)$");
            var goodRegex = new Regex(@"^good (?'year'\d\d\d\d)-(?'month'\d+)$");
            var betterRegex = new Regex(@"^better (?'year'\d\d\d\d).*$");
            //var bestRegex = new Regex(@"^best (?'year'\d\d\d\d)$");

            string targetDirectory;
            if (datedRegex.IsMatch(directoryName)) {
                var match = datedRegex.Match(directoryName);
                var year = match.Groups["year"].Value;
                var month = NormalizeMonth(match.Groups["month"].Value); // strip leading 0
                var day = match.Groups["day"].Value;

                var directory1 = Path.Combine(Path.GetDirectoryName(sourceDirectory),
                    "good " + year + "-" + month);
                var directory2 = Path.Combine(Path.GetDirectoryName(sourceDirectory),
                    "good " + year + "-0" + month);
                if (Directory.Exists(directory1)) {
                    targetDirectory = directory1;
                } else if (Directory.Exists(directory2)) {
                    targetDirectory = directory2;
                } else {
                    // create dir w/ 2 digit month
                    if (month.Length == 1) {
                        targetDirectory = directory2;
                    } else {
                        targetDirectory = directory1;
                    }
                }
            } else if (goodRegex.IsMatch(directoryName)) {
                var match = goodRegex.Match(directoryName);
                var year = match.Groups["year"].Value;
                var month = match.Groups["month"].Value;
                int monthVal = int.Parse(month);
                int quarter;
                switch (monthVal) {
                    case 1:
                    case 2:
                    case 3:
                        quarter = 1; break;
                    case 4:
                    case 5:
                    case 6:
                        quarter = 2; break;
                    case 7:
                    case 8:
                    case 9:
                        quarter = 3; break;
                    case 10:
                    case 11:
                    case 12:
                        quarter = 4; break;
                    default:
                        quarter = 872;
                        break;
                }

                targetDirectory = Path.Combine(Path.GetDirectoryName(sourceDirectory),
                    "better " + year + "-Q" + quarter);
            } else if (betterRegex.IsMatch(directoryName)) {
                var match = betterRegex.Match(directoryName);
                var year = match.Groups["year"].Value;
                var month = match.Groups["month"].Value;

                targetDirectory = Path.Combine(Path.GetDirectoryName(sourceDirectory),
                    "best " + year);
            } else {
                targetDirectory = null;
            }

            return targetDirectory;
        }

        private void SelectDirectory(string directory, TreeView tree)
        {
            SelectDirectory(directory, tree, tree.Items);
        }

        private void SelectDirectory(string directory, TreeView tree, ItemCollection items)
        {
            foreach (var uncastItem in items) {
                var item = (TreeViewItem)uncastItem;
                var prefix = (string)item.DataContext;
                if (StartsWithPrefix(directory, prefix)) {
                    if (Path.Equals(Path.GetFullPath(directory), Path.GetFullPath(prefix))) {
                        if (!item.IsSelected) {
                            if (tree.SelectedItem != null) {
                                var sel = (string)(tree.SelectedItem as FrameworkElement).DataContext;
                                Debug.Assert(directory != sel, "directory exists twice in dialog");
                            }
                            item.IsSelected = true;
                            //tree.SelectedItem = item;
                        }
                        break;
                    } else {
                        ExpandItems(item);
                        item.IsExpanded = true;
                        SelectDirectory(directory, tree, item.Items);
                    }
                }
            }
        }

        // prefix is itself a directly
        private bool StartsWithPrefix(string directory, string prefix)
        {
            if (prefix == null) return false;
            directory = Path.GetFullPath(directory);
            prefix = Path.GetFullPath(prefix);
            if (directory.EndsWith(new string(Path.DirectorySeparatorChar, 1))) {
                // hack to handle the case of "c:\"
                directory = directory.Substring(0, directory.Length - 1);
            }
            if (prefix.EndsWith(new string(Path.DirectorySeparatorChar, 1))) {
                // hack to handle the case of "c:\"
                prefix = prefix.Substring(0, prefix.Length - 1);
            }
            var directoryParts = directory.Split(new char[] { Path.DirectorySeparatorChar });
            var prefixParts = prefix.Split(new char[] { Path.DirectorySeparatorChar });
            if (prefixParts.Length > directoryParts.Length)
                return false;
            for (int i = 0; i < prefixParts.Length; i++) {
                if (directoryParts[i] != prefixParts[i])
                    return false;
            }
            return true;
        }

        private void InitializeTreeView(TreeView tree)
        {
            tree.Items.Clear();
            tree.SelectedItemChanged += new RoutedPropertyChangedEventHandler<object>(tree_SelectedItemChanged);

            // HACK: ToUpperInvariant avoids putting the exact same == directory in the treeview twice, which 
            // SelectFolders can't handle
            var picsDir2 = CreateItem(RootControl.picDir.ToUpperInvariant(), RootControl.picDir);
            tree.Items.Add(picsDir2);

            PopulateNode(picsDir2);
            picsDir2.IsExpanded = true;

    //        var myScreenshots = CreateItem(@"C:\Users\Nick.000\OneDrive\Pictures\Screenshots",
    //@"C:\Users\Nick.000\OneDrive\Pictures\Screenshots");
    //        tree.Items.Add(myScreenshots);

            
            var myPicturesItem = CreateItem(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "My Pictures");
            tree.Items.Add(myPicturesItem);
            
            var cDrive = CreateItem("c:\\", "c:\\");
            tree.Items.Add(cDrive);

            // HACK -- see above
            var fDrive = CreateItem(RootControl.picDrive, RootControl.picDrive);
            tree.Items.Add(fDrive);

            var myDocumentsItem = CreateItem(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Documents");
            tree.Items.Add(myDocumentsItem);
        }

        private TreeViewItem CreateItem(string directory)
        {
            string display = System.IO.Path.GetFileName(directory);
            var item = CreateItem(directory, display);
            return item;
        }

        private TreeViewItem CreateItem(string directory, string display)
        {
            var item = new TreeViewItem();
            item.Style = itemStyle;
            item.Header = display;
            item.DataContext = directory;
            item.Expanded += new RoutedEventHandler(item_Expanded);
            if (!display.StartsWith("<")) {
                // put a dummy node in them so it will have a + next to it, 
                // even before we've examined what's in the directory
                var dummyChild = CreateItem(null, "<dummy item>");
                item.Items.Add(dummyChild);
            }

            return item;
        }

        private void PopulateNode(TreeViewItem parent)
        {
            string startDirectory = (string)parent.DataContext;
            var directories = Directory.GetDirectories(startDirectory);
            if (directories.Length == 0) {
                var item = CreateItem(null, "<empty>");
                parent.Items.Add(item);
            }

            foreach (var directory in directories) {
                var item = CreateItem(directory);
                parent.Items.Add(item);
            }
        }

        private void item_Expanded(object sender, RoutedEventArgs e)
        {
            var item = (TreeViewItem)sender;

            ExpandItems(item);
        }

        private void ExpandItems(TreeViewItem item)
        {
            if (item.Items.Count == 1 && item.Items[0] is TreeViewItem
                && (item.Items[0] as TreeViewItem).Header.Equals("<dummy item>")) {
                item.Items.Clear();
                PopulateNode(item);
            }
        }

        private bool shutdown = false;

        private void tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (shutdown)
                return;

            SourceDirectory = (string)(tree.SelectedItem as TreeViewItem).DataContext;
            var item = (targetTree.SelectedItem as TreeViewItem);
            if (item == null)
                manualTargetDirectory = null;
            else
                manualTargetDirectory = (string)item.DataContext;
        }

        private void ok_Click(object sender, RoutedEventArgs e)
        {
            if (sourceDirectory == TargetDirectory) {
                MessageBox.Show("Source directory and target directory are the same -- you don't want to do that");
                return;
            }

            if (TargetDirectory == null) {
                MessageBox.Show("must select a valid target directory");
            } else {
                this.Canceled = false;
                this.shutdown = true; // for some reason, we get weird selection change notifications when the dialog is closed
                this.Close();
            }
        }

        public bool Canceled = true;

        private void cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();

        }

        private void targetManual_Checked(object sender, RoutedEventArgs e)
        {
            targetTree.IsEnabled = (targetAutomatic.IsChecked == false);
        }

        private void help_Click(object sender, RoutedEventArgs e)
        {
            fileListSource.ShowHelp();
        }
    }
}