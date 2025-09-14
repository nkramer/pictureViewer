using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Diagnostics;
using pictureviewer;

namespace Pictureviewer.Book
{

    public partial class PhotoPageView : UserControl {
        // Using a DependencyProperty as the backing store for Page.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PageProperty =
            DependencyProperty.Register("Page", typeof(PhotoPageModel), typeof(PhotoPageView),
            new UIPropertyMetadata(new PropertyChangedCallback(PageChanged)));

        //// Using a DependencyProperty as the backing store for TemplateName.  This enables animation, styling, binding, etc...
        //public static readonly DependencyProperty TemplateNameProperty =
        //    DependencyProperty.Register("TemplateName", typeof(string), typeof(PhotoPageView), new UIPropertyMetadata(""));

        private static PhotoPageView anyInstance; // for res dict access

        //<local:DroppableImageDisplay local:AspectPreservingGrid.Aspect="Landscape3x2"  ImageIndex="0" Margin="0" Grid.RowSpan="1" Grid.Row="1" Grid.Column="2" Grid.ColumnSpan="1"/>
        //<local:CaptionView Height="Auto" Margin="0,17.4,0,0" VerticalAlignment="Stretch" Grid.Row="2" Grid.Column="2" Grid.RowSpan="2"/>

        // v2 format:
        // [size]_[pic aspect ratio]_[#pics]p[#horiz pics]h[#vert pics]v[#text areas]t
        // (12)=12pixels  m=margin  a=auto (size to content)  g=gutter  *=leftover space
        // iL1121=image Landscape 1row 1rowspan 2col 1colspan
        // c3121=caption 3row 1rowspan 2col 1colspan
        // fL1121=fullbleedImage Landscape 1row 1rowspan 2col 1colspan
        private static readonly string[] templateData = new string[] {
                    //"875x1125_32_1p1h0v1t  m(100)*(100)m mag*m iL2131",
            
            //"875x1125_32_1p1h0v1t  *  * c0101", // test 
            //"875x1125_32_1p1h0v1t  m*m  m*m c1111", // test 
            //"875x1125_32_1p1h0v1t  a a iL0101", // test 
            //"875x1125_32_1p1h0v1t  a a* iP0101 c0111", // test 

            "875x1125_32_1p1h0v1t  mag*m  m(100)*(100)m iL1121 c3121",
            //"875x1125_32_1p1h0v1t mag*m m(100)*(100)m iL1121 c2221",
            //"875x1125_32_2p2h0v1t  *aga*  m*gam iL0323 iL3232 iL3221 c1402", // mis-named & not what original book used. & 3rd image is in a gutter. & layout should fail, but doesn't.
        
            // these three don't use Grid at all...
            //"875x1125_32_1p1h0v0t   iL0101",
            //"875x1125_32_1p1h0v0t_fb * *  fL0101",
            //"875x1125_32_1p1h0v0t_inset   iL0101",

            //"875x1125_32_1p1h0v2t   iL0101 c0101 c0101",
            "875x1125_32_2p0h2v0t_2 *aga* *aga* iP1311 iP1331",

            //// todo
            ////"875x1125_32_2p0h2v1t   border0101 iL0101 iL0101",
            
            "875x1125_32_2p1h1v1t mag*m magam iL1131 iP1111 c3113",
            //"875x1125_32_2p2h0v1t *aga* maga(0) iL1131 iL3131 c1311",
            "875x1125_32_2p2h0v1t (0)aga(0) m*ga(0) iL1131 iL3131 c1311",

            //"875x1125_32_2p2h0v1t_2 *(418.095)g(301.6)* *(278.074)g(625.909)* iL0323 iL3232 iL3231 c1402", // weird
            "875x1125_32_3p0h3v0t *a* *agaga* iP1111 iP1131 iP1151",
            "875x1125_32_3p0h3v1t         mag*m magagam iP1111 iP1131 iP1151 c3115",
            "875x1125_32_3p0h3v1t_hack (55)ag*m magagam iP1111 iP1131 iP1151 c3115",
            "875x1125_32_3p0h3v1t_hack2   mag*m magagagam iP1111 iP1133 iP1171 c3113 c3153", // 2 columns of text -- need 1 more eq to solve it
             "875x1125_32_3p2h1v0t aga aga iP0301 iL0121 iL2121",

            ////"875x1125_32_3p2h1v0t (0)aga(0) (0)aga(0) iP1311 iL1131 iL3131",
            "875x1125_32_3p2h1v0t_2 *aga* *aga* iP1311 iL1131 iL3131",
            
            //"875x1125_32_3p2h1v1t_3 g(490.682)gag g*gaga iL0234 iL3262 iP3231 c0511",
            //"875x1125_32_3p2h1v1t_3_hack g(490.682)gag g*gaga iL0234 iL3262 iP3231 c0511",
            //"875x1125_32_3p3h0v1t (0)aga(0) (0)aga(0) iL1131 iL3131 iL3112 c1111",
            //"875x1125_32_3p3h0v1t_2 *agaga* m*g*gam iL1151 iL3151 iL5151 c1513",
            //"875x1125_32_4p2h2v0t *aga* *(243.334)g(284.166)g(243.334)* iL1113 iP1151 iL3133 iP3111",
            //"875x1125_32_4p2h2v0t_2 *aga* *aga(20.001)a* iP0511 iL1133 iP3151 iP3131",
            //"875x1125_32_4p2h2v0t2 *aga* m(243.334)g(284.166)g(243.334)* iL1113 iP1151 iL3133 iP3111 c1361",
            //"875x1125_32_4p2h2v1t   iL0101 iL0101 iL0101 iP0101 c0101",
            //"875x1125_32_4p2h2v1t_hack   iL0101 iL0101 iL0101 iP0101 c0101",
            "875x1125_32_4p3h1v0t aga agaga iP0101 iL0123 iL2141 iL2103",
            
            //"875x1125_32_4p3h1v0t_2 *aga* *ag(186.856)g(537.822)* iP1111 iL1133 iL3153 iL3113",
            //"875x1125_32_6p0h6v0t *aga* *(218.373333333333)g(218.373333333333)g(218.373333333333)* iP1111 iP1131 iP1151 iP3111 iP3131 iP3151",
            //"875x1125_32_6p0h6v1t *(327.159)g(327.261)* (100)(218.373333333333)g(218.373333333333)g(218.373333333333)g*(100) iP1111 iP1131 iP1151 iP3111 iP3131 iP3151 c1371",
            //"875x1125_32_6p6h0v0t *aga* *(286)g(286)g(286)* iL1111 iL1131 iL1151 iL3111 iL3131 iL3151",
            //"875x1125_32_6p6h0v1t *agaga* *(286)g(286)g(286)* iL1111 iL1131 iL1151 iL3111 iL3131 iL3151 c5115",
            //"875x1125_32_6p6h0v1t_2 *(199.856)g(199.337)g(199.297)* *(286)g(286)g(286)* iL1111 iL1131 iL3111 iL3131 iL5111 iL5131 c1551",
            //"875x1125_32_9p9h0v0t *agaga* *(286)g(286)g(286)* iL1111 iL1131 iL1151 iL3111 iL3131 iL3151 iL5111 iL5131 iL5151",        
};
        // <DataTemplate x:Key="875x1125_32_2p2h0v1t" DataType="local:PhotoPageModel">
        //<local:AspectPreservingGrid Height="875" Width="1125" Background="{Binding BackgroundColor}">

        // name -> full template data (inc. name)
        private static Dictionary<string, string> templateLookup = new Dictionary<string, string>();

        private static Dictionary<string, TemplateDescr> templateLookupV3 = new Dictionary<string, TemplateDescr>();

        static PhotoPageView() {
            // v2 templates
            foreach (string templateDescription in templateData) {
                string[] pieces = templateDescription.Split(' ');
                Debug.Assert(!templateLookup.ContainsKey(pieces[0]));
                templateLookup[pieces[0]] = templateDescription;

                //// print each template into new format
                //Debug.WriteLine(pieces[0] + ":");
                //var view = new PhotoPageView();
                //view.ExpandTemplate(templateDescription);
                //AspectPreservingGrid grid = (AspectPreservingGrid)view.templateContainer.Child;
                //grid.InitializeRowAndColumnDefs(); // hack
                //grid.DebugPrintLayoutAttempted();                
            }

            // v3 templates
            {
                var lines = TemplateStaticDescriptions.data.Split('\n');
                var groups = SplitList(lines.Select(line => line.Trim()).Where(line => !line.StartsWith("//")), line => line == "").Where(group => group.Count() != 0).ToArray();
                IEnumerable<TemplateDescr> descrs = groups.Select(group => new TemplateDescr() { 
                    debugTag = group.First().Replace(":", ""), 
                    lines = group.Skip(1).ToArray() });
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

        //public string TemplateName {
        //    get { return (string)GetValue(TemplateNameProperty); }
        //    set { SetValue(TemplateNameProperty, value); }
        //}

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

        public static string GetTemplateName(DataTemplate t) {
            foreach (object oKey in anyInstance.Resources.Keys) {
                string key = oKey as string;
                if (key != null && anyInstance.Resources[key] is DataTemplate && anyInstance.Resources[key] == t) {
                    return key;
                }
            }
            throw new ArgumentException(" template not found: " + t);
        }

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
        }

        private void ExpandTemplate() {
            // v1, v2, or v3
            if (Page != null) {
                if (templateLookupV3.ContainsKey(Page.TemplateName)) {
                    templateContainer.Child = ParseTemplateV3(templateLookupV3[Page.TemplateName], this.Page);
                } else if (templateLookup.ContainsKey(Page.TemplateName)) {
                    ExpandTemplateV2(templateLookup[Page.TemplateName]);
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

        private static GridLength ParseRowColEntry(string rowColDef) {
            GridLength length;
            if (rowColDef[0] == '(') {
                Debug.Assert(rowColDef.Last() == ')');
                string numStr = rowColDef.Substring(1, rowColDef.Length - 2);
                double num = double.Parse(numStr);
                length = new GridLength(num, GridUnitType.Pixel);
            } else if (rowColDef == "m") {
                length = new GridLength(62.5, GridUnitType.Pixel);
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
                e.Margin = new Thickness(0, 17.4, 0, 0);
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
            string[] colStrs = lines[0].Split(new string[] {" "}, StringSplitOptions.RemoveEmptyEntries);
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
            //Debug.Assert(original == roundTripped);
            string[][] originalText = lines.Select(line => line.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
            string[][] roundTrippedText = roundTripped.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries)).ToArray();

            // assert original = roundTripped except for space
            Debug.Assert(originalText.Zip(roundTrippedText,
                (lines1, lines2) => lines1.Zip(lines2, (s1, s2) => s1 == s2).All(entry => entry))
                .All(entry => entry));

            return p;
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

            return new ExtraConstraint() { RowColA = a, RowColB = b, RowOrColumnA = rc[0], RowOrColumnB=rc[1] };
        }
      
        //       m  (100)      *  (100)      m
        //m      -      -      -      -      -
        //a      -      -     L0      -      -
        //g      -      -      -      -      -
        //*      -      -     C1      -      -
        //m      -      -      -      -      -

        private void ExpandTemplateV2(string templateDescription) {
            var p = new AspectPreservingGrid();
            templateContainer.Child = p;
            //p.Height = 768;
            //p.Width = 1336;
            p.Height = 875;
            p.Width = 1125;
            p.DataContext = Page;
            var binding = new Binding("BackgroundColor");
            p.SetBinding(AspectPreservingGrid.BackgroundProperty, binding);

            string[] pieces = templateDescription.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string templateName = pieces[0];

            List<GridLength> rowDesc = ParseRowColDefinitions(pieces[1]);
            foreach (GridLength gl in rowDesc) {
                var rd = new RowDefinition();
                rd.Height = gl;
                p.RowDefinitions.Add(rd);
            }

            List<GridLength> colDesc = ParseRowColDefinitions(pieces[2]);
            foreach (GridLength gl in colDesc) {
                var cd = new ColumnDefinition();
                cd.Width = gl;
                p.ColumnDefinitions.Add(cd);
            }

            CreateImagesAndCaptions(p, pieces.Skip(3).ToArray(), templateDescription);
        }

        // row or column
        private static List<GridLength> ParseRowColDefinitions(string rowColDef) {
            var rowList = new List<GridLength>();
            int openParen = -1;
            for (int i = 0; i < rowColDef.Length; i++) {
                if (openParen > -1) {
                    if (rowColDef[i] == ')') {
                        string numStr = rowColDef.Substring(openParen + 1, i - (openParen + 1));
                        double num = double.Parse(numStr);
                        GridLength length = new GridLength(num, GridUnitType.Pixel);
                        rowList.Add(length);
                        openParen = -1;
                    }
                } else if (rowColDef[i] == '(') {
                    openParen = i;
                } else {
                    GridLength length;
                    if (rowColDef[i] == 'm') {
                        length = new GridLength(62.5, GridUnitType.Pixel);
                    } else if (rowColDef[i] == 'a') {
                        length = new GridLength(1, GridUnitType.Auto);
                    } else if (rowColDef[i] == 'g') {
                        length = new GridLength(20, GridUnitType.Pixel);
                    } else if (rowColDef[i] == '*') {
                        length = new GridLength(1, GridUnitType.Star);
                    } else {
                        throw new Exception("bad template string");
                    }
                    rowList.Add(length);
                }
            }
            return rowList;
        }

        private static void CreateImagesAndCaptions(AspectPreservingGrid p, string[] pieces, string debugTag) {
            p.Tag = debugTag;
            int imageIndex = 0;
            foreach (string definition in pieces) {
                char[] letters = definition.ToCharArray();
                FrameworkElement elt = null;
                IEnumerable<char> positioning = null;
                if (definition.StartsWith("i")) { // image
                        var e = new DroppableImageDisplay();
                        if (letters[1] == 'L')
                            AspectPreservingGrid.SetAspect(e, Aspect.Landscape3x2);
                        else if (letters[1] == 'P')
                            AspectPreservingGrid.SetAspect(e, Aspect.Portrait2x3);
                        else {
                            throw new Exception("WTF?");
                        }
                        e.ImageIndex = imageIndex;
                        imageIndex++;
                        elt = e;
                        positioning = letters.Skip(2);
                        e.Tag = debugTag + " image " + e.ImageIndex;
                } else if (definition.StartsWith("c")) { // caption
                    var e = new CaptionView();
                    e.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
                    e.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                    elt = e;
                    positioning = letters.Skip(1);
                    e.Margin = new Thickness(0, 17.4, 0, 0);
                }
                p.Children.Add(elt);
                int[] posInts = positioning.Select(c => int.Parse(new String(new char[] { c }))).ToArray();
                Grid.SetRow(elt, posInts[0]);
                Grid.SetRowSpan(elt, posInts[1]);
                Grid.SetColumn(elt, posInts[2]);
                Grid.SetColumnSpan(elt, posInts[3]);
            }
        }
    }
}