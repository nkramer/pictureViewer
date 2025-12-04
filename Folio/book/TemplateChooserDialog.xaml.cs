using Folio.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace Folio.Book {
    // Dialog for choosing a page template
    public partial class TemplateChooserDialog : BaseDialog {
        public string SelectedTemplateName { get; private set; }

        public TemplateChooserDialog(PhotoPageModel currentPage, BookModel book) {
            InitializeComponent();

            // Create sample pages for all templates
            List<PhotoPageModel> samplePages = PhotoPageView.GetAllTemplateNames().Select(name => {
                var sample = new PhotoPageModel(book);
                sample.TemplateName = name;
                // Lorem ipsum text. Does make it slower to bring up template chooser, though.
                sample.RichText = @"<Section xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xml:space='preserve' xml:lang='en-us' Style='{StaticResource BodyBlockStyle}'><Paragraph><Run>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Phasellus auctor dui vel pellentesque ornare. Class aptent taciti sociosqu ad litora torquent per conubia nostra, per inceptos himenaeos. Vestibulum vehicula arcu nec nibh tristique, vitae vulputate erat aliquet. Fusce id metus laoreet, tincidunt lacus tempus, sagittis turpis. Duis vel odio finibus, pellentesque ante sed, molestie velit. Sed pharetra, dui id rhoncus dapibus, tellus risus molestie nisi, at egestas arcu dui sed justo. Nunc maximus, lorem malesuada tristique aliquet, augue justo tristique magna, eu venenatis arcu justo vel arcu. Aliquam pellentesque pretium libero, scelerisque accumsan tortor tincidunt ut. Suspendisse ut massa nec odio dapibus mattis. In nec blandit sapien. Sed ipsum elit, congue et tortor ut, sagittis efficitur mi. Duis tincidunt odio quis ultrices ultricies.</Run></Paragraph><Paragraph><Run>Praesent sit amet mi lorem. Praesent ut dapibus mi. Etiam vel eros sit amet ipsum interdum pretium nec sit amet ex. Praesent sodales elementum metus quis egestas. Sed ex augue, semper sit amet ante nec, commodo tempor dui. Duis varius pellentesque risus, a malesuada nisi mattis eget. In eu est sed sapien scelerisque accumsan.</Run></Paragraph><Paragraph><Run>Nulla vehicula tempor ex, ut vehicula risus sagittis non. Pellentesque urna lacus, sollicitudin ut tincidunt non, sollicitudin eu massa. Vestibulum tincidunt eget urna sit amet tincidunt. Interdum et malesuada fames ac ante ipsum primis in faucibus. Duis pellentesque vehicula tempus. Vivamus id justo convallis, eleifend ipsum quis, scelerisque metus. Vivamus in mattis orci.</Run></Paragraph><Paragraph><Run></Run></Paragraph><Paragraph><Run>Proin faucibus enim vitae turpis aliquet, scelerisque bibendum erat tincidunt. Sed scelerisque dolor ut metus lacinia sodales. Sed bibendum turpis eu imperdiet ullamcorper. Suspendisse sem orci, lobortis vitae accumsan eu, condimentum ut diam. Orci varius natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus. Sed lacinia ullamcorper neque vitae pharetra. Nulla sed pretium tellus. Quisque tempus metus vitae tincidunt posuere. Cras lectus ante, varius sed commodo et, ornare tempus urna.</Run></Paragraph></Section>";
                return sample;
            }).ToList();

            templates.ItemsSource = samplePages;

            // Handle double-click on template to accept immediately
            templates.MouseDoubleClick += (s, e) => {
                if (templates.SelectedItem != null) {
                    OnOk();
                }
            };
        }

        protected override void OnOk() {
            if (templates.SelectedItem != null) {
                SelectedTemplateName = ((PhotoPageModel)templates.SelectedItem).TemplateName;
            }
            base.OnOk();
        }

        protected override void OnCancel() {
            SelectedTemplateName = null;
            base.OnCancel();
        }
    }
}
