using SharpDom.Tests.html5lib.tokenizer;

namespace SharpDom.Tests.html5lib.Tokenizer.OutputTokens
{
    public class Html5LibTokenizerTestOutputEndTagToken : Html5LibTokenizerTestOutputToken
    {
        public string Name { get; init; }
        public override string TokenName => "EndTag";
    }
}