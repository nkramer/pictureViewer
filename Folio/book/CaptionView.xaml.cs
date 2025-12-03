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

        private static Dictionary<TextKind, double> textSizes = new Dictionary<TextKind, double>();
        static CaptionView() {
            // I have no idea where these numbers came from,
            // but they match the corresponding styles in MiscResources.xaml 
            textSizes[TextKind.H1] = 56;
            textSizes[TextKind.H2] = 26.667;
            textSizes[TextKind.Body] = 14.667;
            textSizes[TextKind.Italic] = textSizes[TextKind.Body];
            textSizes[TextKind.Spacer] = 5;
        }

        private CommandHelper commands;
        private RichTextBox box = null;

        // hack for multicolumn
        private int textColumn = 0;
        // Property so you can set it in xaml
        public int TextColumn {
            get { return textColumn; }
            set { textColumn = value; }
        }

        // Returns Model.RichText or Model.RichText2, 
        // depending on the TextColumn. Hack.
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

        private PhotoPageModel Model {
            get {
                return this.DataContext as PhotoPageModel;
            }
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

        private void CaptionView_Loaded(object sender, RoutedEventArgs e) {
            if (Model != null) {
                InitTextFromModel();
                Model.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(Model_PropertyChanged);
            }
        }

        private void CaptionView_Unloaded(object sender, RoutedEventArgs e) {
            if (Model != null) {
                Model.PropertyChanged -= new System.ComponentModel.PropertyChangedEventHandler(Model_PropertyChanged);
            }
        }

        private void Model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == null || e.PropertyName == "RichText") {
                InitTextFromModel();
            }
        }

        private void CaptionView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            InitTextFromModel();
        }

        private void CaptionView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            // Don't enter edit mode when in fullscreen mode, there's no way to exit
            if (Shell.RootControl.Instance.TopScreen is BookViewerFullscreen) {
                return;
            }

            SwitchToRichTextBox(e.GetPosition(stack));
        }

        private void InitTextFromModel() {
            if (Model != null && this.IsLoaded) {
                string xaml = ModelDotRichText;
                if (xaml != null && xaml != "") {
                    this.RichTextXaml = xaml;
                }
            }
        }

        // For performance reasons, we don't use a RichTextBox until someone actually wants to edit.
        // If we use a Rich text box everywhere, the template chooser dialog is slow to come up.
        // but we do need to take more care to keep the different code paths in alignment,
        // particularly around styling and sizing. 
        // 
        // Known discrepancies:
        // - H1's have different left margins, because the TextBlock version uses a negative margin,
        //   and RichTextBox doesn't support that. The negative margin looks better. This could presumably 
        //   be fixed by moving the RichTextBox to the left and adding adding to all the other styles.
        // - Apostrophe s ('s) takes up more space in the RichTextBox path. not sure why that is, maybe it's
        //   a difference in ligatures? 
        private void SwitchToRichTextBox(Point selectionPt) {
            stack.Children.Clear();

            // <RichTextBox x:Name="box" VerticalAlignment="Stretch" FontSize="14.667" Height="Auto" 
            //      Foreground="{Binding ForegroundColor, FallbackValue=white}" Background="{Binding BackgroundColor}" 
            //      BorderBrush="{x:Null}" BorderThickness="0" FontFamily="Segoe" FontWeight="Light" Margin="-5,-5,-5,0" 
            //      Padding="0"  >
            this.box = new RichTextBox();
            stack.Children.Clear();
            stack.Children.Add(box);
            box.VerticalAlignment = VerticalAlignment.Stretch;
            box.FontSize = 14.667;
            box.Height = double.NaN;
            var fgBinding = new Binding("ForegroundColor");
            fgBinding.FallbackValue = Colors.White;
            box.SetBinding(RichTextBox.ForegroundProperty, fgBinding);
            box.SetBinding(RichTextBox.BackgroundProperty, new Binding("BackgroundColor"));
            box.BorderBrush = null;
            box.BorderThickness = new Thickness(0);
            box.FontFamily = new FontFamily("Segoe");
            box.FontWeight = FontWeights.Light;
            box.Margin = new Thickness(-5, 0, -5, 0);  // RichTextBox has a built-in margin that doesn't match TextBlock
            box.Padding = new Thickness(0);
            box.SpellCheck.IsEnabled = true;

            box.LostFocus += new RoutedEventHandler(box_LostFocus);

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

            // I think this commented out code tries to set the selection to match where the user clicked.
            // I don't remember why we didn't use this. 
            //box.Loaded += (object sender, RoutedEventArgs e) => {
            //    box.Focus();
            //
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

        // Apply style to the current paragraph or selected paragraphes.
        // Always works in whole paragraphs. 
        private void ApplyTextStyle(TextKind style) {
            if (box != null && box.IsFocused) {
                // only works if Style set to whole blocks
                string styleName = StyleResourceName(style);
                Style s = (Style)FindResource(styleName);
                Block b = box.Selection.Start.Paragraph;
                while (b != box.Selection.End.Paragraph) {
                    b.Style = s;
                    b = b.NextBlock;
                }
                if (b != null) {
                    b.Style = s;
                }
            }
        }
        private static string StyleResourceName(TextKind style) {
            return style.ToString() + "BlockStyle";
        }

        private void box_LostFocus(object sender, RoutedEventArgs args) {
            if (Model != null) {
                string xaml = this.RichTextXaml;
                ModelDotRichText = xaml;
            }
        }

        // convert RichTextBox's xaml back into something using Styles
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
            if (xaml == "" || xaml == null) return "";
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

    }
}