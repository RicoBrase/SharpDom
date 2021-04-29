namespace SharpDom.Tokenization.Tokens
{
    public abstract class HtmlToken
    {
        public abstract HtmlTokenType Type { get; }

        public abstract override string ToString();
    }
}