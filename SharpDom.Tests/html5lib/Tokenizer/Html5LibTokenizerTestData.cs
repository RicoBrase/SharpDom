using System;
using SharpDom.Tests.html5lib.Tokenizer.OutputTokens;

namespace SharpDom.Tests.html5lib.tokenizer
{
    public class Html5LibTokenizerTestData
    {
        public string Index { get; set; }
        public string Description { get; init; }
        public string Input { get; init; }
        public string[] InitialStates { get; init; }
        public Html5LibTokenizerTestOutputToken[] Output { get; init; }

        public Html5LibTokenizerTestData()
        {
            InitialStates = Array.Empty<string>();
        }

        public override string ToString()
        {
            return $"{Index} ➡ {Description}";
        }
    }
}