using SharpDom.Tokenization;

namespace SharpDom.Tests.html5lib.Tokenizer.OutputTokens
{
    public class Html5LibTokenizerTestOutputDoctypeToken : Html5LibTokenizerTestOutputToken
    {
        public string Name { get; init; }
        public string PublicId { get; init; }
        public string SystemId { get; init; }
        /// <summary>
        /// Correctness corresponds to the force-quirks flag being false and vice-versa. <br/>
        /// <code>Correctness = !ForceQuirks</code>
        /// </summary>
        public bool Correctness { get; init; }

        public override string TokenName => "DOCTYPE";
        public override HtmlTokenType TokenType => HtmlTokenType.Doctype;
    }
}