using FluentAssertions;
using SharpDom.Utils;
using Xunit;

namespace SharpDom.Tests.Utils
{
    public class OptionalTests
    {
        [Fact]
        public void Empty_ShouldReturnEmptyOptional()
        {
            // Arrange
            var sut = Optional<string>.Empty();

            // Act
            var isEmpty = sut.HasValue;

            // Assert
            isEmpty.Should().BeFalse();
        }

        [Fact]
        public void Of_ShouldReturnOptionalWithValue()
        {
            // Arrange
            var sut = Optional<string>.Of("foo");
            
            // Act
            var isEmpty = sut.HasValue;

            // Assert
            isEmpty.Should().BeTrue();
        }

        [Fact]
        public void TryGet_ShouldOutputValueAndReturnTrue_IfNotEmpty()
        {
            // Arrange
            const string val = "foo";
            var sut = Optional<string>.Of(val);

            // Act
            var getWasSuccessful = sut.TryGet(out var sutValue);

            // Assert
            getWasSuccessful.Should().BeTrue();
            sutValue.Should().BeEquivalentTo(val);
        }
        
        [Fact]
        public void TryGet_ShouldOutputDefaultValueAndReturnFalse_IfEmpty()
        {
            // Arrange
            var sut = Optional<string>.Empty();

            // Act
            var getWasSuccessful = sut.TryGet(out var sutValue);

            // Assert
            getWasSuccessful.Should().BeFalse();
            sutValue.Should().BeEquivalentTo(default);
        }
        
    }
}