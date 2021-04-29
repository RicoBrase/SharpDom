namespace SharpDom.Tokenization.Tokens
{
    public class HtmlEndTagToken : HtmlTagToken
    {
        public override HtmlTokenType Type => HtmlTokenType.EndTag;
        
        public override string ToString()
        {
            return $"HtmlEndTagToken {{ {base.ToString()} }}";
        }
    }
}