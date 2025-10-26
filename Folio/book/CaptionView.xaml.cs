using Folio.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

namespace Folio.Book {
    public static class EnumerableExt {
        public static IEnumerable<IEnumerable<T>> Partition<T>(this IEnumerable<T> input, Func<T, bool> test) {
            var enumerator = input.GetEnumerator();

            while (enumerator.MoveNext()) {
                yield return nextPartition(enumerator, test);
            }
        }

        private static IEnumerable<T> nextPartition<T>(IEnumerator<T> enumerator, Func<T, bool> test) {
            do {
                yield return enumerator.Current;
            }
            while (!test(enumerator.Current));
        }

        public static IEnumerable<IEnumerable<T>> SplitBeforeIf<T>(
            this IEnumerable<T> source, Func<T, bool> predicate) {
            var temp = new List<T>();

            foreach (var item in source)
                if (predicate(item)) {
                    if (temp.Any())
                        yield return temp;

                    temp = new List<T> { item };
                } else
                    temp.Add(item);

            yield return temp;
        }
    }


    // Captions and other text in a photo book 
    public partial class CaptionView : UserControl {
        public enum TextKind {
            H1,
            H2,
            Body,
            Italic,
            Spacer, // hack
            None,
        }

        private CommandHelper commands;
        private RichTextBox box = null;
        private static Dictionary<TextKind, double> textSizes = new Dictionary<TextKind, double>();

        static CaptionView() {
            textSizes[TextKind.H1] = 56;
            textSizes[TextKind.H2] = 26.667;
            textSizes[TextKind.Body] = 14.667;
            textSizes[TextKind.Italic] = textSizes[TextKind.Body];
            textSizes[TextKind.Spacer] = 5;
        }

        public CaptionView() {
            InitializeComponent();
            this.Loaded += new RoutedEventHandler(CaptionView_Loaded);
            this.Unloaded += new RoutedEventHandler(CaptionView_Unloaded);
            this.DataContextChanged += new DependencyPropertyChangedEventHandler(CaptionView_DataContextChanged);
            this.MouseLeftButtonDown += new MouseButtonEventHandler(CaptionView_MouseLeftButtonDown);

            this.commands = new CommandHelper(this, true);

            Command command;

            command = new Command();
            command.Text = "H1";
            command.HasMenuItem = false;
            command.Key = Key.D1;
            command.Execute += delegate () {
                ApplyTextStyle(TextKind.H1);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "H2";
            command.HasMenuItem = false;
            command.Key = Key.D2;
            command.Execute += delegate () {
                ApplyTextStyle(TextKind.H2);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Body";
            command.HasMenuItem = false;
            command.Key = Key.D3;
            command.Execute += delegate () {
                ApplyTextStyle(TextKind.Body);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Body";
            command.HasMenuItem = false;
            command.Key = Key.D0;
            command.Execute += delegate () {
                ApplyTextStyle(TextKind.Body);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Italic";
            command.HasMenuItem = false;
            command.Key = Key.I;
            command.Execute += delegate () {
                ApplyTextStyle(TextKind.Italic);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Italic";
            command.HasMenuItem = false;
            command.Key = Key.D4;
            command.Execute += delegate () {
                ApplyTextStyle(TextKind.Italic);
            };
            commands.AddCommand(command);

            command = new Command();
            command.Text = "Spacer";
            command.HasMenuItem = false;
            command.Key = Key.D5;
            command.Execute += delegate () {
                ApplyTextStyle(TextKind.Spacer);
            };
            commands.AddCommand(command);
        }

        void CaptionView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            SwitchToRichTextBox(e.GetPosition(stack));
        }

        private void SwitchToRichTextBox(Point selectionPt) {
            stack.Children.Clear();
            //<RichTextBox x:Name="box" VerticalAlignment="Stretch" FontSize="14.667" Height="Auto" 
            //         Foreground="{Binding ForegroundColor, FallbackValue=white}" Background="{Binding BackgroundColor}" 
            //         BorderBrush="{x:Null}" BorderThickness="0" FontFamily="Segoe" FontWeight="Light" Margin="-5,-5,-5,0" 
            //Padding="0"  >-->
            this.box = new RichTextBox();
            stack.Children.Clear();
            stack.Children.Add(box);
            box.VerticalAlignment = VerticalAlignment.Stretch;
            box.FontSize = 14.667;
            box.Height = double.NaN;
            var fgBinding = new Binding("ForegroundColor");
            fgBinding.FallbackValue = Colors.White;
            //box.SetBinding(RichTextBox.ForegroundProperty, fgBinding);
            //box.Foreground = Brushes.Red;
            box.Foreground = Brushes.Blue;
            box.SetBinding(RichTextBox.BackgroundProperty, new Binding("BackgroundColor"));
            box.BorderBrush = null;
            box.BorderThickness = new Thickness(0);
            box.FontFamily = new FontFamily("Segoe");
            box.FontWeight = FontWeights.Light;
            box.Margin = new Thickness(-5, 0, -5, 0);  // TextBox has built-in margin that doesn't match TextBlock
            box.Padding = new Thickness(0);
            box.SpellCheck.IsEnabled = true;

            box.LostFocus += new RoutedEventHandler(box_LostFocus);
            box.PreviewKeyDown += new KeyEventHandler(box_KeyDown);
            //box.KeyDown += new KeyEventHandler(box_KeyDown);

            string xaml = ModelDotRichText;
            xaml = FakeToRealXaml(xaml);
            var doc = new FlowDocument();
            box.Document = doc; // faster to parent before populating
            if (xaml != "") {
                var range = new TextRange(doc.ContentStart, doc.ContentEnd);
                range.Load(new MemoryStream(Encoding.UTF8.GetBytes(xaml)), DataFormats.Xaml);
            }

            box.Focus();
            box.Selection.Select(box.Document.ContentEnd, box.Document.ContentEnd);
            //box.Loaded += (object sender, RoutedEventArgs e) => {
            //    box.Focus();

            //    TextPointer textPointer = box.Document.ContentStart;
            //    while (true) {
            //        Rect rect = textPointer.GetCharacterRect(LogicalDirection.Forward);
            //        if (rect.Contains(selectionPt)) {
            //            break;
            //        }
            //        textPointer = textPointer.GetNextContextPosition(LogicalDirection.Forward);
            //        if (textPointer == null) {
            //            textPointer = box.Document.ContentEnd;
            //            break;
            //        }
            //    }
            //    box.Selection.Select(textPointer, textPointer);
            //};

            //box.s
            //box.SelectAll();
            //box.Selection.Select(
        }

        // hack for multicolumn
        private int textColumn = 0;
        // Property so you can set it in xaml
        public int TextColumn {
            get { return textColumn; }
            set { textColumn = value; }
        }

        // hackorama
        private string ModelDotRichText {
            get {
                var s = (textColumn == 0) ? Model.RichText : Model.RichText2;
                return s;
            }
            set {
                if (textColumn == 0) {
                    Model.RichText = value;
                } else {
                    Model.RichText2 = value;
                }
            }
        }

        private void ApplyTextStyle(TextKind style) {
            if (box != null && box.IsFocused) {
                // only works if Style set to whole blocks
                string styleName = StyleResourceName(style);
                Style s = (Style)FindResource(styleName);
                Block b = box.Selection.Start.Paragraph;
                while (b != box.Selection.End.Paragraph) {
                    b.Style = s;
                    //b.Margin = new Thickness(0, 0, 0, 10);
                    b = b.NextBlock;
                }
                if (b != null) {
                    b.Style = s;
                }
                //b.Margin = new Thickness(0, 0, 0, 10);

                //box.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, textSizes[style]);
                ////box.Selection.GetPropertyValue(TextElement.FontStyleProperty);
                //FontStyle s = (style == TextKind.Italic) ? FontStyles.Italic : FontStyles.Normal;
                //box.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, s);

                //Block b = box.Selection.Start.Paragraph;
                //while (b != box.Selection.End.Paragraph) {
                //    b.Margin = new Thickness(0, 0, 0, 10);
                //    b = b.NextBlock;                
                //}
                //b.Margin = new Thickness(0, 0, 0, 10);
            }
        }
        private static string StyleResourceName(TextKind style) {
            return style.ToString() + "BlockStyle";
        }

        private void box_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.T & Keyboard.Modifiers == ModifierKeys.Control) {
                box.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 60.0);
                e.Handled = true;
            }
        }

        private void box_LostFocus(object sender, RoutedEventArgs args) {
            if (Model != null) {
                string xaml = this.RichTextXaml;
                ModelDotRichText = xaml;
            }
        }

        // convert RTB's xaml back into something using Styles
        private static string RealXamlToFake(string xaml) {
            XDocument d = XDocument.Parse(xaml);
            var meaningfulAttrs = new string[] { "xmlns", /*xml:*/ "space", /*xml:*/ "lang", };
            foreach (XElement e in d.Descendants()) {
                TextKind kind = TextKind.None;
                if (e.Attributes("FontSize").Count() > 0) {
                    double size = double.Parse(e.Attributes("FontSize").First().Value);
                    foreach (TextKind k in textSizes.Keys.OrderBy(k => k)) {
                        if (size > textSizes[k] - 0.5 && size < textSizes[k] + 0.5) {
                            kind = k;
                            break;
                        }
                    }
                    // None isn't an error, the formatting just gets stripped
                    // Debug.Assert(kind != TextKind.None);
                }
                if (e.Attribute("FontStyle") != null && (e.Attribute("FontStyle").Value as string) != "Normal") {
                    kind = TextKind.Italic;
                }
                var attrsToRemove =
                    e.Attributes()
                    .Select(a => a.Name.LocalName)
                    .Where(s => !meaningfulAttrs.Contains(s))
                    .ToArray();
                foreach (string s in attrsToRemove) {
                    e.Attribute(s).Remove();
                }
                if (kind != TextKind.None && e.Name.LocalName != "Run" && e.Name.LocalName != "Span") {
                    e.Add(new XAttribute("Style", "{StaticResource " + StyleResourceName(kind) + "}"));
                }
            }
            xaml = d.ToString(SaveOptions.DisableFormatting);
            return xaml;
        }

        // todo: once legacy content converted, function has no purpose
        private string FakeToRealXaml(string xaml) {
            if (xaml == "") return "";
            XDocument d = XDocument.Parse(xaml);
            foreach (XElement e in d.Descendants()) {
                if (e.Attributes("pv-TextKind").Count() > 0) {
                    TextKind kind = (TextKind)Enum.Parse(typeof(TextKind), e.Attributes("pv-TextKind").First().Value);
                    e.Attributes("pv-TextKind").Remove();
                    //e.Add(new XAttribute("FontSize", textSizes[kind].ToString()));
                    //if (kind == TextKind.Italic) 
                    //    e.Add(new XAttribute("FontStyle", "Italic"));
                }
            }
            xaml = d.ToString(SaveOptions.DisableFormatting);
            return xaml;
        }

        // Deals in "fake xaml", sends it to textbox
        private string RichTextXaml {
            get {
                Debug.Assert(box != null);
                MemoryStream buffer = new MemoryStream();
                var doc = box.Document;
                var range = new TextRange(doc.ContentStart, doc.ContentEnd);
                range.Save(buffer, DataFormats.Xaml);
                string xaml = Encoding.UTF8.GetString(buffer.ToArray());
                xaml = RealXamlToFake(xaml);
                return xaml;
            }
            set {
                string xaml = value;
                stack.Children.Clear();
                XDocument d = XDocument.Parse(xaml);
                Debug.Assert(d.Root.Name.LocalName == "Section");
                foreach (XElement p in d.Root.Elements()) {
                    Debug.Assert(p.Name.LocalName == "Paragraph");
                    var chunks = p.Elements().SplitBeforeIf(r => r.Name.LocalName == "LineBreak");
                    Debug.Assert(p.Elements().All(elt => elt.Name.LocalName == "Run" || elt.Name.LocalName == "Span" || elt.Name.LocalName == "LineBreak"));
                    int i = -1;
                    foreach (IEnumerable<XElement> chunk in chunks) {
                        i++;
                        var tb = new TextBlock();
                        stack.Children.Add(tb);
                        string text = chunk.Select(elt => elt.Value).Aggregate("", (a, b) => a + b);
                        tb.Text = text;
                        tb.TextWrapping = TextWrapping.Wrap;

                        Binding binding = new Binding("ForegroundColor");
                        binding.FallbackValue = Brushes.White;
                        tb.SetBinding(TextBlock.ForegroundProperty, binding);

                        XAttribute styleAttr = p.Attribute("Style");
                        if (styleAttr != null) {
                            string styleName = styleAttr.Value.Replace("{StaticResource ", "").Replace("}", "").Trim();
                            styleName = styleName.Replace("BlockStyle", "TextBlockStyle");
                            tb.Style = (Style)FindResource(styleName);
                            //if (styleName == "H1TextBlockStyle") {
                            //    tb.Margin = new Thickness(-5, -5, -5, 0);
                            //}
                        } else {
                            tb.Style = (Style)FindResource("BodyTextBlockStyle");
                        }
                        if (i < chunks.Count() - 1) {   // not last chunk
                            tb.Margin = new Thickness(0, 0, 0, 0);
                        }
                    }
                }
            }
        }

        private PhotoPageModel Model {
            get {
                return this.DataContext as PhotoPageModel;
            }
        }

        private void CaptionView_Loaded(object sender, RoutedEventArgs e) {
            if (Model != null) {
                InitTextFromModel();
                Model.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(Model_PropertyChanged);
            }
        }

        void CaptionView_Unloaded(object sender, RoutedEventArgs e) {
            if (Model != null) {
                Model.PropertyChanged -= new System.ComponentModel.PropertyChangedEventHandler(Model_PropertyChanged);
            }
        }

        private void Model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == null || e.PropertyName == "RichText") {
                InitTextFromModel();
            }
        }

        void CaptionView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            InitTextFromModel();
        }

        private void InitTextFromModel() {
            if (Model != null) {
                if (this.IsLoaded) {
                    string xaml = ModelDotRichText;
                    if (xaml != null && xaml != "") {
                        this.RichTextXaml = xaml;
                    }
                }
            }
        }
    }
}