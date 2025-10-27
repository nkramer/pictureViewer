using FluentAssertions;
using Folio.Core;
using Xunit;

namespace Folio.Tests.Core {
    public class RatioTests {
        [Fact]
        public void Constructor_ShouldSetNumeratorAndDenominator() {
            var ratio = new Ratio(3, 4);
            ratio.numerator.Should().Be(3);
            ratio.denominator.Should().Be(4);
        }

        [Fact]
        public void ToString_WhenDenominatorIsOne_ShouldReturnOnlyNumerator() {
            var ratio = new Ratio(5, 1);
            var result = ratio.ToString();
            result.Should().Be("5");
        }

        [Fact]
        public void ToString_WhenDenominatorIsNotOne_ShouldReturnFraction() {
            var ratio = new Ratio(3, 4);
            var result = ratio.ToString();
            result.Should().Be("3/4");
        }

        [Fact]
        public void Parse_ShouldParseSimpleNumber() {
            var ratio = Ratio.Parse("5");
            ratio.numerator.Should().Be(5);
            ratio.denominator.Should().Be(1);
        }

        [Fact]
        public void Parse_ShouldParseFraction() {
            var ratio = Ratio.Parse("3/4");
            ratio.numerator.Should().Be(3);
            ratio.denominator.Should().Be(4);
        }

        [Fact]
        public void Parse_WithMultipleSlashes_ShouldThrowArgumentException() {
            Assert.Throws<System.ArgumentException>(() => Ratio.Parse("1/2/3"));
        }

        [Fact]
        public void Parse_WithEmptyString_ShouldThrowFormatException() {
            Assert.Throws<System.FormatException>(() => Ratio.Parse(""));
        }

        [Fact]
        public void IsValid_WhenBothNumeratorAndDenominatorAreValid_ShouldReturnTrue() {
            var ratio = new Ratio(3, 4);
            ratio.IsValid.Should().BeTrue();
        }

        [Fact]
        public void IsValid_WhenInvalid_ShouldReturnFalse() {
            var ratio = Ratio.Invalid;
            ratio.IsValid.Should().BeFalse();
        }

        [Fact]
        public void Invalid_ShouldHaveNegativeOneValues() {
            var invalid = Ratio.Invalid;
            invalid.numerator.Should().Be(-1);
            invalid.denominator.Should().Be(-1);
        }

        [Fact]
        public void Parse_ThenToString_ShouldRoundTrip() {
            var original = "3/4";
            var ratio = Ratio.Parse(original);
            var result = ratio.ToString();
            result.Should().Be(original);
        }
    }
}
