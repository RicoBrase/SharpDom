namespace SharpDom.Tokenization.Tokens
{
    public class HtmlEndOfFileToken : HtmlToken
    {
        public override HtmlTokenType Type => HtmlTokenType.EndOfFile;
        
        public override string ToString()
        {
            return "HtmlEndOfFileToken { }";
        }
    }
}