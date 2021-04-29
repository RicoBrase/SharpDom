namespace SharpDom.Tests.html5lib.Tokenizer.OutputTokens
{
    public class Html5LibTokenizerTestOutputCommentToken : Html5LibTokenizerTestOutputToken
    {
        public string Data { get; init; }
        public override string TokenName => "Comment";
    }
}