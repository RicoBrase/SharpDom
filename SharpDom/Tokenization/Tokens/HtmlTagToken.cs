using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpDom.Tokenization.Tokens
{
    public abstract class HtmlTagToken : HtmlToken
    {
        
        public string TagName { get; set; }
        public bool SelfClosing { get; set; }
        public bool SelfClosingAcknowledged { get; set; }
        public List<Attribute> Attributes { get; }

        public Attribute CurrentAttribute => Attributes.Count == 0 ? StartNewAttribute() : Attributes.Last();

        public HtmlTagToken()
        {
            SelfClosing = false;
            SelfClosingAcknowledged = false;
            Attributes = new List<Attribute>();
        }

        public Attribute StartNewAttribute()
        {
            var a = Attribute.New();
            Attributes.Add(a);
            return a;
        }
        
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"TagName = {TagName}, SelfClosing = {SelfClosing}, SelfClosingAcknowledged = {SelfClosingAcknowledged}, Attributes = {{ ");
            foreach (var attribute in Attributes)
            {
                sb.Append($"{attribute}, ");
            }

            sb.Append(" }");
            return sb.ToString();
        }
    }
}