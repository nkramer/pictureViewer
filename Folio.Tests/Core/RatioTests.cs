using FluentAssertions;
using Folio.Core;
using Xunit;

namespace Folio.Tests.Core
{
    public class RatioTests
    {
        [Fact]
        public void Constructor_ShouldSetNumeratorAndDenominator()
        {
            // Arrange & Act
            var ratio = new Ratio(3, 4);

            // Assert
            ratio.numerator.Should().Be(3);
            ratio.denominator.Should().Be(4);
        }

        [Fact]
        public void ToString_WhenDenominatorIsOne_ShouldReturnOnlyNumerator()
        {
            // Arrange
            var ratio = new Ratio(5, 1);

            // Act
            var result = ratio.ToString();

            // Assert
            result.Should().Be("5");
        }

        [Fact]
        public void ToString_WhenDenominatorIsNotOne_ShouldReturnFraction()
        {
            // Arrange
            var ratio = new Ratio(3, 4);

            // Act
            var result = ratio.ToString();

            // Assert
            result.Should().Be("3/4");
        }

        [Fact]
        public void Parse_ShouldParseSimpleNumber()
        {
            // Act
            var ratio = Ratio.Parse("5");

            // Assert
            ratio.numerator.Should().Be(5);
            ratio.denominator.Should().Be(1);
        }

        [Fact]
        public void Parse_ShouldParseFraction()
        {
            // Act
            var ratio = Ratio.Parse("3/4");

            // Assert
            ratio.numerator.Should().Be(3);
            ratio.denominator.Should().Be(4);
        }

        [Fact]
        public void Parse_WithMultipleSlashes_ShouldThrowArgumentException()
        {
            // Act & Assert
            Assert.Throws<System.ArgumentException>(() => Ratio.Parse("1/2/3"));
        }

        [Fact]
        public void Parse_WithEmptyString_ShouldThrowFormatException()
        {
            // Act & Assert
            Assert.Throws<System.FormatException>(() => Ratio.Parse(""));
        }

        [Fact]
        public void IsValid_WhenBothNumeratorAndDenominatorAreValid_ShouldReturnTrue()
        {
            // Arrange
            var ratio = new Ratio(3, 4);

            // Act & Assert
            ratio.IsValid.Should().BeTrue();
        }

        [Fact]
        public void IsValid_WhenInvalid_ShouldReturnFalse()
        {
            // Arrange
            var ratio = Ratio.Invalid;

            // Act & Assert
            ratio.IsValid.Should().BeFalse();
        }

        [Fact]
        public void Invalid_ShouldHaveNegativeOneValues()
        {
            // Arrange & Act
            var invalid = Ratio.Invalid;

            // Assert
            invalid.numerator.Should().Be(-1);
            invalid.denominator.Should().Be(-1);
        }

        [Fact]
        public void Parse_ThenToString_ShouldRoundTrip()
        {
            // Arrange
            var original = "3/4";

            // Act
            var ratio = Ratio.Parse(original);
            var result = ratio.ToString();

            // Assert
            result.Should().Be(original);
        }
    }
}
