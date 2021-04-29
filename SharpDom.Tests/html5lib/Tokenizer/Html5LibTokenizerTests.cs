using System.Linq;
using FluentAssertions;
using SharpDom.Tokenization;
using Xunit;

namespace SharpDom.Tests.html5lib.tokenizer
{
    public class Html5LibTokenizerTests
    {

        [Theory]
        [Html5LibTokenizerTestData("html5lib-tests/tokenizer/test1.test")]
        public void Test1(Html5LibTokenizerTestData testCase)
        {
            // Arrange
            var tokenizer = new HtmlTokenizer(testCase.Input);
            
            // Act
            var tokenizationResult = tokenizer.Run();
            var actualTokens = tokenizationResult.Tokens.Where(token => token.Type != HtmlTokenType.EndOfFile).ToArray();
            var actualErrors = tokenizationResult.Errors.ToArray();

            // Assert
            actualTokens.Should().HaveCount(testCase.Output.Length);
            for (var i = 0; i < testCase.Output.Length; i++)
            {
                var expectedToken = testCase.Output[i];
                var actualToken = actualTokens[i];
                
            }
        }
        
    }
}