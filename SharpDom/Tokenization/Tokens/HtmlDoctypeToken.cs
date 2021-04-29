using System.Text;
using SharpDom.Utils;

namespace SharpDom.Tokenization.Tokens
{
    public class HtmlDoctypeToken : HtmlToken
    {
        public override HtmlTokenType Type => HtmlTokenType.Doctype;

        public Optional<string> Name { get; set; }
        public Optional<string> PublicIdentifier { get; set; }
        public Optional<string> SystemIdentifier { get; set; }
        public bool ForceQuirks { get; set; }
        
        public HtmlDoctypeToken()
        {
            Name = Optional<string>.Empty();
            PublicIdentifier = Optional<string>.Empty();
            SystemIdentifier = Optional<string>.Empty();
            ForceQuirks = false;
        }
        
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("HtmlDoctypeToken { ");
            if (Name.TryGet(out var nameVal)) sb.Append($"Name = {nameVal}, ");
            if (PublicIdentifier.TryGet(out var publicIdentifierVal)) sb.Append($"PublicIdentifier = {publicIdentifierVal}, ");
            if (SystemIdentifier.TryGet(out var systemIdentifierVal)) sb.Append($"SystemIdentifier = {systemIdentifierVal}, ");
            sb.Append($"ForceQuirks = {ForceQuirks}");
            sb.Append(" }");
            return sb.ToString();
        }
    }
}