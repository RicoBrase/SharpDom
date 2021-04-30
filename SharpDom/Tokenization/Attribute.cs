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

        public override bool Equals(object obj)
        {
            if (obj is Attribute attr)
            {
                return KeyBuilder.ToString().Equals(attr.KeyBuilder.ToString()) &&
                       ValueBuilder.ToString().Equals(attr.ValueBuilder.ToString());
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(KeyBuilder, ValueBuilder);
        }
    }
}