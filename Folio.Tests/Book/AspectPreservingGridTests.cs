using FluentAssertions;
using Folio.Book;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using Xunit;

namespace Folio.Tests.Book
{
    public class AspectPreservingGridTests
    {
        [Fact]
        public void ComputeSizes_ShouldHandleAllTemplatesWithoutThrowing()
        {
            Exception testException = null;
            var thread = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null)
                    {
                        new Application();
                    }
                    var app = Application.Current;
                    var miscResources = new ResourceDictionary { Source = new Uri("pack://application:,,,/Folio;component/assets/MiscResources.xaml") };
                    var templates = new ResourceDictionary { Source = new Uri("pack://application:,,,/Folio;component/assets/Templates_875x1125.xaml") };
                    var samples = new ResourceDictionary { Source = new Uri("pack://application:,,,/Folio;component/assets/Templates_Samples.xaml") };
                    var wpfTemplates = new ResourceDictionary { Source = new Uri("pack://application:,,,/Folio;component/assets/WpfControlTemplates.xaml") };
                    app.Resources.MergedDictionaries.Add(miscResources);
                    app.Resources.MergedDictionaries.Add(templates);
                    app.Resources.MergedDictionaries.Add(samples);
                    app.Resources.MergedDictionaries.Add(wpfTemplates);
                    var templateNames = PhotoPageView.GetAllTemplateNames().ToList();
                    templateNames.Should().NotBeEmpty("there should be at least one template");
                    foreach (var templateName in templateNames)
                    {
                        var bookModel = new BookModel();
                        var pageModel = new PhotoPageModel(bookModel) { TemplateName = templateName };
                        var pageView = new PhotoPageView { Page = pageModel };
                        var testSize = new Size(1125, 875);
                        var exception = Record.Exception(() => pageView.Measure(testSize));
                        exception.Should().BeNull($"template '{templateName}' should not throw during layout");
                    }
                }
                catch (Exception ex)
                {
                    testException = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (testException != null)
            {
                throw testException;
            }
        }
    }
}
