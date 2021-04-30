using SharpDom.Tokenization;

namespace SharpDom.Tests.html5lib.Tokenizer.OutputTokens
{
    public abstract class Html5LibTokenizerTestOutputToken
    {
        public abstract string TokenName { get; }
        public abstract HtmlTokenType TokenType { get; }
    }
}