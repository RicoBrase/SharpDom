using System.Text;

namespace SharpDom.Tokenization.Tokens
{
    public class HtmlCommentToken : HtmlToken
    {
        public override HtmlTokenType Type => HtmlTokenType.Comment;

        public string Data { get; set; }

        public override string ToString()
        {
            return $"HtmlCommentToken {{ data = {Data} }}";
        }
    }
}