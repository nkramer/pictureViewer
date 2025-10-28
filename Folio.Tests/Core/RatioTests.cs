using FluentAssertions;
using Folio.Core;
using Xunit;

namespace Folio.Tests.Core {
    public class RatioTests {
        [Fact]
        public void BasicBehavior() {
            new Ratio(3, 4).numerator.Should().Be(3);
            new Ratio(3, 4).denominator.Should().Be(4);
            new Ratio(5, 1).ToString().Should().Be("5");
            new Ratio(3, 4).ToString().Should().Be("3/4");
            Ratio.Parse("5").numerator.Should().Be(5);
            Ratio.Parse("5").denominator.Should().Be(1);
            Ratio.Parse("3/4").numerator.Should().Be(3);
            Ratio.Parse("3/4").denominator.Should().Be(4);
            Ratio.Parse("3/4").ToString().Should().Be("3/4");
            new Ratio(3, 4).IsValid.Should().BeTrue();
            Ratio.Invalid.IsValid.Should().BeFalse();
            Ratio.Invalid.numerator.Should().Be(-1);
            Ratio.Invalid.denominator.Should().Be(-1);
            Assert.Throws<System.ArgumentException>(() => Ratio.Parse("1/2/3"));
            Assert.Throws<System.FormatException>(() => Ratio.Parse(""));
        }
    }
}
