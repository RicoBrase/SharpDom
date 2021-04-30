using System;
using SharpDom.Tests.html5lib.Tokenizer.OutputTokens;

namespace SharpDom.Tests.html5lib.tokenizer
{
    public class Html5LibTokenizerTestData
    {
        public string Index { get; set; }
        public string Description { get; init; }
        public string Input { get; init; }
        public string InitialState { get; init; }
        public Html5LibTokenizerTestOutputToken[] Output { get; init; }
        public Html5LibTokenizerTestError[] Errors { get; init; }

        public Html5LibTokenizerTestData()
        {
            Output = Array.Empty<Html5LibTokenizerTestOutputToken>();
            Errors = Array.Empty<Html5LibTokenizerTestError>();
        }

        public override string ToString()
        {
            return $"{Index}{(!string.IsNullOrEmpty(InitialState) ? $"[state={InitialState}]" : "")} ➡ {Description}";
        }
    }
}