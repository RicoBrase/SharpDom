using System.Text;

namespace SharpDom.Tokenization
{
    public class Attribute
    {
        public StringBuilder KeyBuilder { get; }
        public StringBuilder ValueBuilder { get; }
        
        private Attribute()
        {
            KeyBuilder = new StringBuilder();
            ValueBuilder = new StringBuilder();
        }

        public static Attribute New()
        {
            return new();
        }

        public override string ToString()
        {
            return $"{KeyBuilder} = {ValueBuilder}";
        }
    }
}