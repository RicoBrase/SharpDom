using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using SharpDom.Tests.html5lib.Tokenizer.OutputTokens;
using SharpDom.Tokenization;
using SharpDom.Tokenization.Tokens;
using Xunit;
using Attribute = SharpDom.Tokenization.Attribute;

namespace SharpDom.Tests.html5lib.tokenizer
{
    public class Html5LibTokenizerTests
    {

        [Theory]
        [Html5LibTokenizerTestData("html5lib-tests/tokenizer/test1.test")]
        public void Test1(Html5LibTokenizerTestData testCase)
        {
            // Arrange
            var initialState = testCase.InitialState switch
            {
                "PLAINTEXT state" => HtmlTokenizerState.PlainText,
                "RCDATA state" => HtmlTokenizerState.RcData,
                "RAWTEXT state" => HtmlTokenizerState.RawText,
                "Script data state" => HtmlTokenizerState.ScriptData,
                "CDATA section state" => HtmlTokenizerState.CDataSection,
                _ => HtmlTokenizerState.Data
            };
            var tokenizer = new HtmlTokenizer(testCase.Input, true, initialState);

            // Act
            var tokenizationResult = tokenizer.Run();
            var actualTokens = tokenizationResult.Tokens.Where(token => token.Type != HtmlTokenType.EndOfFile).ToArray();
            var actualErrors = tokenizationResult.Errors.ToArray();

            // Assert
            // Output / Tokens
            actualTokens.Should().HaveCount(testCase.Output.Length);
            for (var i = 0; i < testCase.Output.Length; i++)
            {
                var expectedToken = testCase.Output[i];
                var actualToken = actualTokens[i];
                actualToken.Type.Should().Be(expectedToken.TokenType);
                switch (expectedToken.TokenType)
                {
                    case HtmlTokenType.Doctype:
                        DoctypeTokensMatch((HtmlDoctypeToken) actualToken, (Html5LibTokenizerTestOutputDoctypeToken)expectedToken);
                        break;
                    case HtmlTokenType.StartTag:
                        StartTagTokensMatch((HtmlStartTagToken) actualToken, (Html5LibTokenizerTestOutputStartTagToken)expectedToken);
                        break;
                    case HtmlTokenType.EndTag:
                        EndTagTokensMatch((HtmlEndTagToken) actualToken, (Html5LibTokenizerTestOutputEndTagToken)expectedToken);
                        break;
                    case HtmlTokenType.Comment:
                        CommentTagTokensMatch((HtmlCommentToken) actualToken, (Html5LibTokenizerTestOutputCommentToken)expectedToken);
                        break;
                    case HtmlTokenType.Character:
                        CharacterTagTokensMatch((HtmlCharacterToken) actualToken, (Html5LibTokenizerTestOutputCharacterToken)expectedToken);
                        break;
                    case HtmlTokenType.EndOfFile:
                        true.Should().BeFalse("there are no EndOfFile tokens to be expected.");
                        break;
                    default:
                        true.Should().BeFalse("default should not be reached.");
                        break;
                }
            }
            
            // Errors
            actualErrors.Should().HaveCount(testCase.Errors.Length);
            for (var i = 0; i < testCase.Errors.Length; i++)
            {
                actualErrors[i].ToString().Should().Be(testCase.Errors[i].Code);
            }
        }

        private static void DoctypeTokensMatch(HtmlDoctypeToken actualToken,
            Html5LibTokenizerTestOutputDoctypeToken expectedToken)
        {
            // Name
            actualToken.Name.HasValue.Should().BeTrue();
            actualToken.Name.TryGet(out var actualTokenName);
            actualTokenName.Should().Be(expectedToken.Name);

            // PublicIdentifier
            if (expectedToken.PublicId == null)
            {
                actualToken.PublicIdentifier.HasValue.Should().BeFalse();
            }
            else
            {
                actualToken.PublicIdentifier.TryGet(out var actualTokenPublicId);
                actualTokenPublicId.Should().Be(expectedToken.PublicId);
            }
            
            // SystemIdentifier
            if (expectedToken.SystemId == null)
            {
                actualToken.SystemIdentifier.HasValue.Should().BeFalse();
            }
            else
            {
                actualToken.SystemIdentifier.TryGet(out var actualTokenSystemId);
                actualTokenSystemId.Should().Be(expectedToken.SystemId);
            }
            
            // ForceQuirks
            actualToken.ForceQuirks.Should().Be(!expectedToken.Correctness);
        }

        private static void StartTagTokensMatch(HtmlStartTagToken actualToken,
            Html5LibTokenizerTestOutputStartTagToken expectedToken)
        {
            var expectedAttributes = new List<Attribute>();
            foreach (var (key, value) in expectedToken.Attributes)
            {
                var attribute = Attribute.New();
                attribute.KeyBuilder.Append(key);
                attribute.ValueBuilder.Append(value);
                expectedAttributes.Add(attribute);
            }
            
            // Name
            actualToken.TagName.Should().Be(expectedToken.Name);
            
            // Attributes
            actualToken.Attributes.Should().Equal(expectedAttributes);

            // SelfClosing
            actualToken.SelfClosing.Should().Be(expectedToken.SelfClosing);
        }
        
        private static void EndTagTokensMatch(HtmlEndTagToken actualToken,
            Html5LibTokenizerTestOutputEndTagToken expectedToken)
        {
            // Name
            actualToken.TagName.Should().Be(expectedToken.Name);
        }
        
        private static void CommentTagTokensMatch(HtmlCommentToken actualToken,
            Html5LibTokenizerTestOutputCommentToken expectedToken)
        {
            // Data
            actualToken.Data.Should().Be(expectedToken.Data);
        }
        
        private static void CharacterTagTokensMatch(HtmlCharacterToken actualToken,
            Html5LibTokenizerTestOutputCharacterToken expectedToken)
        {
            // Data
            actualToken.Data.Should().Be(expectedToken.Data);
        }

    }
}