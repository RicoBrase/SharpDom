using System.Collections.Generic;
using SharpDom.Tokenization.Tokens;

namespace SharpDom.Tokenization
{
    public class HtmlTokenizationResult
    {
        public IEnumerable<HtmlToken> Tokens { get; init; }
        public IEnumerable<HtmlParseError> Errors { get; init; }
    }
}