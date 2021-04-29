using System.Collections.Generic;
using SharpDom.Tests.html5lib.tokenizer;

namespace SharpDom.Tests.html5lib.Tokenizer.OutputTokens
{
    public class Html5LibTokenizerTestOutputStartTagToken : Html5LibTokenizerTestOutputToken
    {
        public string Name { get; init; }
        public Dictionary<string, string> Attributes { get; }
        public bool SelfClosing { get; init; }

        public override string TokenName => "StartTag";
        
        public Html5LibTokenizerTestOutputStartTagToken()
        {
            Attributes = new Dictionary<string, string>();
        }
    }
}