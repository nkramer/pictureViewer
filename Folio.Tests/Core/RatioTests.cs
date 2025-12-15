using FluentAssertions;
using Folio.Core;
using Xunit;

namespace Folio.Tests.Core; 
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
        Ratio.Invalid.numerator.Should().Be(0);
        Ratio.Invalid.denominator.Should().Be(0);
        Assert.Throws<System.ArgumentException>(() => Ratio.Parse("1/2/3"));
        Assert.Throws<System.ArgumentException>(() => Ratio.Parse(""));
    }

    [Fact]
    public void Simplification_ShouldReduceToLowestTerms() {
        // 8/6 should be simplified to 4/3
        var ratio = new Ratio(8, 6);
        ratio.numerator.Should().Be(4);
        ratio.denominator.Should().Be(3);

        // 10/15 should be simplified to 2/3
        ratio = new Ratio(10, 15);
        ratio.numerator.Should().Be(2);
        ratio.denominator.Should().Be(3);

        // 100/50 should be simplified to 2/1
        ratio = new Ratio(100, 50);
        ratio.numerator.Should().Be(2);
        ratio.denominator.Should().Be(1);

        // Already simplified ratios should stay the same
        ratio = new Ratio(3, 4);
        ratio.numerator.Should().Be(3);
        ratio.denominator.Should().Be(4);

        // 0/5 should be simplified to 0/1
        ratio = new Ratio(0, 5);
        ratio.numerator.Should().Be(0);
        ratio.denominator.Should().Be(1);
    }

    [Fact]
    public void Simplification_WithNegativeNumbers() {
        // Negative numerator should be preserved
        var ratio = new Ratio(-8, 6);
        ratio.numerator.Should().Be(-4);
        ratio.denominator.Should().Be(3);

        // Negative denominator should move sign to numerator
        ratio = new Ratio(8, -6);
        ratio.numerator.Should().Be(-4);
        ratio.denominator.Should().Be(3);

        // Double negative should result in positive
        ratio = new Ratio(-8, -6);
        ratio.numerator.Should().Be(4);
        ratio.denominator.Should().Be(3);
    }

    [Fact]
    public void Constructor_ShouldThrowOnZeroDenominator() {
        Assert.Throws<System.ArgumentException>(() => new Ratio(5, 0));
    }

    [Fact]
    public void Equals_SimplifiedRatiosAreEqual() {
        var ratio1 = new Ratio(8, 6);
        var ratio2 = new Ratio(4, 3);

        ratio1.Equals(ratio2).Should().BeTrue();
        ratio2.Equals(ratio1).Should().BeTrue();
        ratio1.Equals((object)ratio2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentRatiosAreNotEqual() {
        var ratio1 = new Ratio(3, 4);
        var ratio2 = new Ratio(4, 5);

        ratio1.Equals(ratio2).Should().BeFalse();
        ratio2.Equals(ratio1).Should().BeFalse();
    }

    [Fact]
    public void Equals_SameInstanceIsEqual() {
        var ratio = new Ratio(3, 4);
        ratio.Equals(ratio).Should().BeTrue();
    }

    [Fact]
    public void Equals_NullIsNotEqual() {
        var ratio = new Ratio(3, 4);
        ratio.Equals(null).Should().BeFalse();
        ratio.Equals((object)null).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SimplifiedRatiosHaveSameHashCode() {
        var ratio1 = new Ratio(8, 6);
        var ratio2 = new Ratio(4, 3);

        // Equal ratios should have equal hash codes
        ratio1.GetHashCode().Should().Be(ratio2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentRatiosCanHaveDifferentHashCodes() {
        var ratio1 = new Ratio(3, 4);
        var ratio2 = new Ratio(4, 5);

        // Different ratios typically have different hash codes (not guaranteed, but likely)
        // We just test that GetHashCode returns a value without throwing
        var hash1 = ratio1.GetHashCode();
        var hash2 = ratio2.GetHashCode();

        // At least verify they return consistent values
        ratio1.GetHashCode().Should().Be(hash1);
        ratio2.GetHashCode().Should().Be(hash2);
    }

    [Fact]
    public void Parse_SimplifiesResultingRatio() {
        // Parsing "8/6" should result in simplified 4/3
        var ratio = Ratio.Parse("8/6");
        ratio.numerator.Should().Be(4);
        ratio.denominator.Should().Be(3);
    }

    [Fact]
    public void Invalid_DoesNotGetSimplified() {
        // Invalid ratio should remain 0/0 and not be simplified
        Ratio.Invalid.numerator.Should().Be(0);
        Ratio.Invalid.denominator.Should().Be(0);
    }

    [Fact]
    public void ZeroNumerator_WithValidDenominator_IsValidRatio() {
        // 0/5 should be valid and simplify to 0/1
        var ratio = new Ratio(0, 5);
        ratio.IsValid.Should().BeTrue();
        ratio.numerator.Should().Be(0);
        ratio.denominator.Should().Be(1);

        // This should be different from Invalid (0/0)
        ratio.Equals(Ratio.Invalid).Should().BeFalse();
    }

    [Fact]
    public void ZeroDenominator_WithNonZeroNumerator_ThrowsException() {
        // x/0 where x != 0 should throw
        Assert.Throws<System.ArgumentException>(() => new Ratio(5, 0));
        Assert.Throws<System.ArgumentException>(() => new Ratio(-3, 0));
    }

    [Fact]
    public void Parse_ColonFormat_CreatesRatio() {
        // Parsing "3:2" should create 3/2 ratio
        var ratio = Ratio.Parse("3:2");
        ratio.numerator.Should().Be(3);
        ratio.denominator.Should().Be(2);

        // Parsing "2:3" should create 2/3 ratio
        ratio = Ratio.Parse("2:3");
        ratio.numerator.Should().Be(2);
        ratio.denominator.Should().Be(3);

        // Parsing "16:9" should create 16/9 ratio
        ratio = Ratio.Parse("16:9");
        ratio.numerator.Should().Be(16);
        ratio.denominator.Should().Be(9);
    }

    [Fact]
    public void Parse_ColonFormat_SimplifiesRatio() {
        // Parsing "8:6" should simplify to 4/3
        var ratio = Ratio.Parse("8:6");
        ratio.numerator.Should().Be(4);
        ratio.denominator.Should().Be(3);

        // Parsing "10:15" should simplify to 2/3
        ratio = Ratio.Parse("10:15");
        ratio.numerator.Should().Be(2);
        ratio.denominator.Should().Be(3);
    }

    [Fact]
    public void Parse_BothFormats_ProduceSameResult() {
        // Both colon and slash formats should produce same result
        var ratioColon = Ratio.Parse("4:3");
        var ratioSlash = Ratio.Parse("4/3");

        ratioColon.numerator.Should().Be(ratioSlash.numerator);
        ratioColon.denominator.Should().Be(ratioSlash.denominator);
        ratioColon.Equals(ratioSlash).Should().BeTrue();
    }
}
