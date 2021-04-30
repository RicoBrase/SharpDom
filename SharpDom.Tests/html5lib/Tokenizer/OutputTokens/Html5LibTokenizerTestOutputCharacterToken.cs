using SharpDom.Tokenization;

namespace SharpDom.Tests.html5lib.Tokenizer.OutputTokens
{
    public class Html5LibTokenizerTestOutputCharacterToken : Html5LibTokenizerTestOutputToken
    {
        public string Data { get; init; }
        public override string TokenName => "Character";
        public override HtmlTokenType TokenType => HtmlTokenType.Character;
    }
}