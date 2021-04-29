using Xunit;
using FluentAssertions;
using SharpDom.Infra.Unicode;

namespace SharpDom.Tests.Infra.Unicode
{
    public class CodepointTests
    {

        [Theory]
        [InlineData(0xD7FF, false)]
        [InlineData(0xD800, true)]
        [InlineData(0xDFFF, true)]
        [InlineData(0xE000, false)]
        public void IsSurrogate_ShouldReturnTrue_IfCodepointIsSurrogate(int code, bool expected)
        {
            // Arrange
            var codepoint = Codepoint.Get(code);

            // Act
            var result = codepoint.IsSurrogate();

            // Assert
            result.Should().Be(expected);
        }

    }
}