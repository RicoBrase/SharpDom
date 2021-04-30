using SharpDom.Tests.html5lib.tokenizer;
using SharpDom.Tokenization;

namespace SharpDom.Tests.html5lib.Tokenizer.OutputTokens
{
    public class Html5LibTokenizerTestOutputEndTagToken : Html5LibTokenizerTestOutputToken
    {
        public string Name { get; init; }
        public override string TokenName => "EndTag";
        public override HtmlTokenType TokenType => HtmlTokenType.EndTag;
    }
}