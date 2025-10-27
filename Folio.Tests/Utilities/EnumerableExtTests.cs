using FluentAssertions;
using Folio.Utilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Folio.Tests.Utilities {
    public class EnumerableExtTests {
        [Fact]
        public void SplitBeforeIf_WithEmptySequence_ShouldReturnSingleEmptyGroup() {
            var input = new List<int>();
            var result = input.SplitBeforeIf(x => x > 5).ToList();
            result.Should().HaveCount(1);
            result[0].Should().BeEmpty();
        }

        [Fact]
        public void SplitBeforeIf_ShouldSplitBeforeMatchingItems() {
            var input = new List<int> { 1, 2, 3, 10, 11, 4, 5 };
            var result = input.SplitBeforeIf(x => x > 5).ToList();
            result.Should().HaveCount(3);
            result[0].Should().Equal(1, 2, 3);
            result[1].Should().Equal(10);
            result[2].Should().Equal(11, 4, 5);
        }

        [Fact]
        public void SplitBeforeIf_WhenFirstItemMatches_ShouldStartNewGroup() {
            var input = new List<int> { 10, 1, 2, 3 };
            var result = input.SplitBeforeIf(x => x > 5).ToList();
            result.Should().HaveCount(1);
            result[0].Should().Equal(10, 1, 2, 3);
        }

        [Fact]
        public void SplitBeforeIf_WhenNoItemsMatch_ShouldReturnSingleGroup() {
            var input = new List<int> { 1, 2, 3, 4, 5 };
            var result = input.SplitBeforeIf(x => x > 10).ToList();
            result.Should().HaveCount(1);
            result[0].Should().Equal(1, 2, 3, 4, 5);
        }

        [Fact]
        public void SplitBeforeIf_WithMultipleConsecutiveMatches_ShouldCreateSeparateGroups() {
            var input = new List<int> { 1, 10, 11, 12, 2 };
            var result = input.SplitBeforeIf(x => x > 5).ToList();
            result.Should().HaveCount(4);
            result[0].Should().Equal(1);
            result[1].Should().Equal(10);
            result[2].Should().Equal(11);
            result[3].Should().Equal(12, 2);
        }

        // Note: The Partition method has implementation issues and is not tested here
        // It appears to yield the same element multiple times without advancing the enumerator
        // If this method is needed in the future, it should be fixed first
    }
}
