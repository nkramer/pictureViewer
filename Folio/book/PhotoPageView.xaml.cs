using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Folio.Book {

    public partial class PhotoPageView : UserControl {
        // Using a DependencyProperty as the backing store for Page.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PageProperty =
            DependencyProperty.Register("Page", typeof(PhotoPageModel), typeof(PhotoPageView),
                new UIPropertyMetadata(new PropertyChangedCallback(PageChanged)));

        // We're only doing this to get change notifications from Page.TemplateName, there's cleaner
        // ways of doing it but this works.
        public static readonly DependencyProperty TemplateNameProperty =
            DependencyProperty.Register("TemplateName", typeof(string), typeof(PhotoPageView),
                new UIPropertyMetadata(new PropertyChangedCallback(TemplateNameChanged)));

        private static PhotoPageView anyInstance; // for res dict access
        private static Dictionary<string, TemplateDescr> templateLookupV3 = new Dictionary<string, TemplateDescr>();

        static PhotoPageView() {
            // v3 templates
            {
                var lines = TemplateStaticDescriptions.data.Split('\n');
                var groups = SplitList(lines.Select(line => line.Trim()).Where(line => !line.StartsWith("//")), line => line == "").Where(group => group.Count() != 0).ToArray();
                IEnumerable<TemplateDescr> descrs = groups.Select(group => new TemplateDescr() {
                    debugTag = group.First().Replace(":", ""),
                    lines = group.Skip(1).ToArray()
                });
                foreach (var d in descrs) {
                    Debug.Assert(!templateLookupV3.ContainsKey(d.debugTag));
                    templateLookupV3[d.debugTag] = d;
                }

                //templateLookupV3 = .ToDictionary(templateDescr => templateDescr.debugTag); // no debug info!
            }
        }

        private static IEnumerable<IEnumerable<T>> SplitList<T>(IEnumerable<T> input, Func<T, bool> splitHere) {
            var result = new List<List<T>>();
            var currentGroup = new List<T>();
            result.Add(currentGroup);
            foreach (T elt in input) {
                if (splitHere(elt)) {
                    currentGroup = new List<T>();
                    result.Add(currentGroup);
                } else {
                    currentGroup.Add(elt);
                }
            }
            return result;
        }

        public bool IsPrintMode = false;

        public PhotoPageView() {
            InitializeComponent();
            if (anyInstance == null)
                anyInstance = this;
            this.DataContextChanged += new DependencyPropertyChangedEventHandler(PhotoPageView_DataContextChanged);
            this.Loaded += new RoutedEventHandler(PhotoPageView_Loaded);
        }

        public PhotoPageModel Page {
            get { return (PhotoPageModel)GetValue(PageProperty); }
            set { SetValue(PageProperty, value); }
        }

        public string TemplateName {
            get { return (string)GetValue(TemplateNameProperty); }
            set { SetValue(TemplateNameProperty, value); }
        }

        public static IEnumerable<string> GetAllTemplateNames() {
            ResourceDictionary dictionary = App.Current.Resources.MergedDictionaries[1];
            //anyInstance.Resources.MergedDictionaries[0];
            IEnumerable<string> keys = dictionary.Keys.OfType<string>();
            List<string> list = keys.Where(s => {
                var t = dictionary[s] as DataTemplate;
                if (t == null) return false;
                var foo = (string)t.DataType;
                bool res = t != null && (t.DataType as string) == "local:PhotoPageModel";
                return res;
            }).ToList();
            list.Sort();
            return list;
        }

        public static DataTemplate GetTemplate(string name) {
            ResourceDictionary dictionary = App.Current.Resources;
            //ResourceDictionary dictionary = App.Current.Resources.MergedDictionaries[0];
            //anyInstance.Resources.MergedDictionaries[0];
            object r = dictionary[name];
            var t = (DataTemplate)r;
            Debug.Assert(t != null);
            Debug.Assert((t.DataType as string) == "local:PhotoPageModel");
            return t;

            //foreach (object oKey in anyInstance.Resources.Keys) {
            //    string key = oKey as string;
            //    if (key != null && anyInstance.Resources[key] is DataTemplate && key == name) {
            //        return (DataTemplate)anyInstance.Resources[key];
            //    }
            //}
            //return null;
        }

        public static string TemplateVersion(string name) {
            if (templateLookupV3.ContainsKey(name))
                return "v3 ";
            else
                return "v1 ";
        }

        //public static string GetTemplateName(DataTemplate t) {
        //    foreach (object oKey in anyInstance.Resources.Keys) {
        //        string key = oKey as string;
        //        if (key != null && anyInstance.Resources[key] is DataTemplate && anyInstance.Resources[key] == t) {
        //            return key;
        //        }
        //    }
        //    throw new ArgumentException(" template not found: " + t);
        //}

        private void PhotoPageView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (this.DataContext != null && Page != null) {
                ExpandTemplate();
            }
        }

        private void PhotoPageView_Loaded(object sender, RoutedEventArgs e) {
            ExpandTemplate();
        }

        private static void PageChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args) {
            ((PhotoPageView)obj).ExpandTemplate();

            var pageview = (PhotoPageView)obj;
            BindingOperations.ClearBinding(pageview, TemplateNameProperty);
            if (pageview.Page != null) {
                var binding = new Binding(nameof(pageview.Page.TemplateName)) {
                    Source = pageview.Page,
                    Mode = BindingMode.OneWay
                };
                BindingOperations.SetBinding(pageview, TemplateNameProperty, binding);
            }
        }

        private static void TemplateNameChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args) {
            ((PhotoPageView)obj).ExpandTemplate();
        }

        private void ExpandTemplate() {
            // v1, v2, or v3
            if (Page != null) {
                if (templateLookupV3.ContainsKey(Page.TemplateName)) {
                    templateContainer.Child = ParseTemplateV3(templateLookupV3[Page.TemplateName], this.Page);
                } else {
                    DataTemplate t = (DataTemplate)this.TryFindResource(Page.TemplateName);
                    if (t != null) {
                        FrameworkElement content = (FrameworkElement)t.LoadContent();
                        templateContainer.Child = content;
                    }
                    // disable while getting templates working
                    //Debug.Fail("how'd that happen?");
                }
            }
        }

        // v3
        private static GridLength ParseRowColEntry(string rowColDef) {
            GridLength length;
            if (rowColDef[0] == '(') {
                Debug.Assert(rowColDef.Last() == ')');
                string numStr = rowColDef.Substring(1, rowColDef.Length - 2);
                double num = double.Parse(numStr);
                length = new GridLength(num, GridUnitType.Pixel);
            } else if (rowColDef == "m") {
                length = new GridLength(50, GridUnitType.Pixel);
            } else if (rowColDef == "a") {
                length = new GridLength(1, GridUnitType.Auto);
            } else if (rowColDef == "g") {
                length = new GridLength(20, GridUnitType.Pixel);
            } else if (rowColDef == "*") {
                length = new GridLength(1, GridUnitType.Star);
            } else {
                throw new Exception("bad template string");
            }
            return length;
        }

        private static UIElement CreateImagesAndCaptions(char type, int index, string debugTag) {
            FrameworkElement elt = null;
            if (type == 'L' || type == 'P') {
                var e = new DroppableImageDisplay();
                if (type == 'L')
                    AspectPreservingGrid.SetAspect(e, Aspect.Landscape3x2);
                else if (type == 'P')
                    AspectPreservingGrid.SetAspect(e, Aspect.Portrait2x3);
                else {
                    throw new Exception("WTF?");
                }
                e.ImageIndex = index;
                e.Tag = debugTag + " image " + e.ImageIndex;
                elt = e;
            } else if (type == 'C') {
                var e = new CaptionView();
                e.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
                e.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                // e.Margin = new Thickness(0, 17.4, 0, 0);
                elt = e;
            } else {
                Debug.Fail("unknown " + type);
            }
            return elt;
        }

        //private class ChildDescription {
        //    public char type;
        //    public int rowspan;
        //    public int colspan;
        //}

        private class TemplateDescr {
            public string[] lines;
            public string debugTag;
        }

        private static UIElement ParseTemplateV3(TemplateDescr templateDescr, PhotoPageModel model) {
            var p = new AspectPreservingGrid();
            //p.Height = 768;
            //p.Width = 1336;
            p.Height = 875;
            p.Width = 1125;
            p.DataContext = model;
            var binding = new Binding("BackgroundColor");
            p.SetBinding(AspectPreservingGrid.BackgroundProperty, binding);
            p.Tag = templateDescr.debugTag;

            p.ExtraConstraints = templateDescr.lines.Where(line => line.Trim().StartsWith("and")).Select(line => ParseExtraConstraint(line.Trim())).ToArray();

            string[] lines = templateDescr.lines.Where(line => !line.Trim().StartsWith("and")).ToArray();
            Debug.Assert(lines.Length > 1);
            string[] colStrs = lines[0].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            GridLength[] cols = colStrs.Select(s => ParseRowColEntry(s)).ToArray();
            string[] rowStrs = lines.Skip(1).Select(line => line.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).First()).ToArray();
            GridLength[] rows = rowStrs.Select(s => ParseRowColEntry(s)).ToArray();
            string[][] cellStrs = lines.Skip(1).Select(line => line.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray()).ToArray();

            //var table = new Dictionary<int, ChildDescription>();

            for (int index = 0; index < 10; index++) {
                var cellBools = cellStrs.Select((line, rownum) => line.Select((cell, colnum) => new { isTarget = cell != "-" && index == int.Parse(cell.Substring(1)), rownum = rownum, colnum = colnum, str = cell }).ToArray()).ToArray();
                var childShape = cellBools.Select(line => line.Where(c => c.isTarget).ToArray()).Where(line => line.Count() > 0).ToArray();
                if (childShape.Count() > 0) {
                    int rowStart = childShape.First().First().rownum;
                    int rowSpan = childShape.Count();
                    int colStart = childShape.First().First().colnum;
                    int colSpan = childShape.First().Count();

                    var sample = childShape.First().First();
                    var elt = CreateImagesAndCaptions(sample.str[0], index, templateDescr.debugTag);
                    Grid.SetRow(elt, rowStart);
                    Grid.SetRowSpan(elt, rowSpan);
                    Grid.SetColumn(elt, colStart);
                    Grid.SetColumnSpan(elt, colSpan);
                    p.Children.Add(elt);
                }
            }
            int numCaptions = 0;
            foreach (UIElement child in p.Children) {
                if (child is CaptionView) {
                    ((CaptionView)child).TextColumn = numCaptions;
                    numCaptions++;
                }
            }

            foreach (GridLength gl in rows) {
                var rd = new RowDefinition();
                rd.Height = gl;
                p.RowDefinitions.Add(rd);
            }

            foreach (GridLength gl in cols) {
                var cd = new ColumnDefinition();
                cd.Width = gl;
                p.ColumnDefinitions.Add(cd);
            }

            //foreach (string[] row in cellStrs) {
            //    foreach (string cell in row) {
            //        char type = cell[0];
            //        int index = int.Parse(cell.Substring(1));
            //        if (table.ContainsKey(index))
            //            table[index] = new ChildDescription() { type = type, colspan = 1, rowspan = 1 };
            //        else {
            //            ChildDescription old = table[index];
            //            Debug.Assert(type == old.type);
            //            table[index] = new ChildDescription() { type = old.type, colspan = old.colspan, rowspan = old.rowspan };
            //        }
            //    }
            //}

            // validate results
            p.InitializeRowAndColumnDefs(); // hack
            string roundTripped = p.LayoutToString().Trim();
            string original = string.Join("\n", lines.Select(line => line.Trim())).Trim();
            string[][] originalText = lines.Select(line => line.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
            string[][] roundTrippedText = roundTripped.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries)).ToArray();

            // assert original = roundTripped except for space
            if (!originalText.Zip(roundTrippedText,
                (lines1, lines2) => lines1.Zip(lines2, (s1, s2) => s1 == s2).All(entry => entry))
                .All(entry => entry)) {

                Debug.WriteLine("originalText");
                //Debug.WriteLine(originalText.Join);
                PrintArrayOfArrays(originalText);
                Debug.WriteLine("roundTrippedText");
                PrintArrayOfArrays(roundTrippedText);
            }

            Debug.Assert(originalText.Zip(roundTrippedText,
                (lines1, lines2) => lines1.Zip(lines2, (s1, s2) => s1 == s2).All(entry => entry))
                .All(entry => entry));

            return p;
        }

        private static void PrintArrayOfArrays(string[][] arrayOfArrays) {
            foreach (var innerArray in arrayOfArrays) {
                Debug.WriteLine(string.Join(", ", innerArray));
            }
        }

        private static ExtraConstraint ParseExtraConstraint(string line) {
            //and row0=row4
            string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Debug.Assert(parts.Length == 2 && parts[0] == "and");
            string[] words = parts[1].Split(new char[] { '=' });
            Debug.Assert(words.Length == 2);
            int a = int.Parse(words[0].Last().ToString());
            int b = int.Parse(words[1].Last().ToString());
            RowOrColumn[] rc = words.Select(word => {
                RowOrColumn rowOrCol = RowOrColumn.Row;

                if (words[0].StartsWith("row"))
                    rowOrCol = RowOrColumn.Row;
                else if (words[0].StartsWith("col"))
                    rowOrCol = RowOrColumn.Column;
                else
                    Debug.Fail("unknown row/col string");
                return rowOrCol;
            }).ToArray();

            return new ExtraConstraint() { RowColA = a, RowColB = b, RowOrColumnA = rc[0], RowOrColumnB = rc[1] };
        }

        //       m  (100)      *  (100)      m
        //m      -      -      -      -      -
        //a      -      -     L0      -      -
        //g      -      -      -      -      -
        //*      -      -     C1      -      -
        //m      -      -      -      -      -
    }
}