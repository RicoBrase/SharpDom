using System;

namespace SharpDom.Tokenization
{
    public class HtmlParseError
    {
        public HtmlParseErrorType Type { get; init; }

        public override string ToString()
        {
            return $"HtmlParseError {{ Type = {Enum.GetName(Type)} }}";
        }
    }
}