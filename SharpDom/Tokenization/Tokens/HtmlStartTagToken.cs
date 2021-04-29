namespace SharpDom.Tokenization.Tokens
{
    public class HtmlStartTagToken : HtmlTagToken
    {
        public override HtmlTokenType Type => HtmlTokenType.StartTag;
        
        public override string ToString()
        {
            return $"HtmlStartTagToken {{ {base.ToString()} }}";
        }
    }
}