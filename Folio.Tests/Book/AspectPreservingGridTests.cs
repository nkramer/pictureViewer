using FluentAssertions;
using Folio.Book;
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Xunit;
using Xunit.Abstractions;

namespace Folio.Tests.Book
{
    public class AspectPreservingGridTests
    {
        private readonly ITestOutputHelper _output;

        public AspectPreservingGridTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ComputeSizes_ShouldHandleAllTemplatesWithoutThrowing()
        {
            var failures = new System.Collections.Generic.List<string>();
            var successes = new System.Collections.Generic.List<string>();
            Exception setupException = null;
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
                        try
                        {
                            var bookModel = new BookModel();
                            var pageModel = new PhotoPageModel(bookModel) { TemplateName = templateName };
                            var grid = PhotoPageView.APGridFromTemplate(templateName, pageModel);
                            if (grid != null) {
                                //grid.ComputeSizes(new Size(1125, 875));
                                grid.ComputeSizes(new Size(1336, 768));
                            }
                        }
                        catch (Exception ex)
                        {
                            failures.Add($"{templateName}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    setupException = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (setupException != null)
            {
                throw setupException;
            }
            if (failures.Count > 0)
            {
                var failureReport = $"Failed templates ({failures.Count}/{failures.Count + successes.Count}):\n" +
                                  string.Join("\n", failures) +
                                  $"\n\nSucceeded ({successes.Count}/{failures.Count + successes.Count}):\n" +
                                  string.Join("\n", successes);
                _output.WriteLine(failureReport);
                Assert.Fail(failureReport);
            }
        }
    }
}
