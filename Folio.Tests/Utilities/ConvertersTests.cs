using FluentAssertions;
using Folio.Utilities;
using System.Windows;
using Xunit;

namespace Folio.Tests.Utilities
{
    public class BoolToVisibilityConverterTests
    {
        private readonly BoolToVisibilityConverter _converter = new BoolToVisibilityConverter();

        [Fact]
        public void Convert_WhenTrue_ShouldReturnVisible()
        {
            // Act
            var result = _converter.Convert(true, null, null, null);

            // Assert
            result.Should().Be(Visibility.Visible);
        }

        [Fact]
        public void Convert_WhenFalse_ShouldReturnCollapsed()
        {
            // Act
            var result = _converter.Convert(false, null, null, null);

            // Assert
            result.Should().Be(Visibility.Collapsed);
        }
    }

    public class BoolToScaleFlipConverterTests
    {
        private readonly BoolToScaleFlipConverter _converter = new BoolToScaleFlipConverter();

        [Fact]
        public void Convert_WhenTrue_ShouldReturnNegativeOne()
        {
            // Act
            var result = _converter.Convert(true, null, null, null);

            // Assert
            result.Should().Be(-1.0);
        }

        [Fact]
        public void Convert_WhenFalse_ShouldReturnOne()
        {
            // Act
            var result = _converter.Convert(false, null, null, null);

            // Assert
            result.Should().Be(1.0);
        }
    }
}
