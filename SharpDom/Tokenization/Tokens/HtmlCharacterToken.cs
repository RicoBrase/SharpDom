using System.Text;
using System.Text.RegularExpressions;
using SharpDom.Infra.Unicode;

namespace SharpDom.Tokenization.Tokens
{
    public class HtmlCharacterToken : HtmlToken
    {
        public override HtmlTokenType Type => HtmlTokenType.Character;
        public string Data { get; set; }

        public override string ToString()
        {
            return $"HtmlCharacterToken {{ data = {Regex.Escape(Data)} }}";
        }
    }
}