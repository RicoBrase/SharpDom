namespace SharpDom.Utils
{
    public class Optional<TData>
    {
        private readonly TData _value;
        
        public bool HasValue { get; }

        private Optional()
        {
            _value = default;
            HasValue = false;
        }
        
        private Optional(TData value)
        {
            _value = value;
            HasValue = true;
        }

        public bool TryGet(out TData val)
        {
            val = _value;
            return HasValue;
        }

        public static Optional<TData> Of(TData item)
        {
            return new(item);
        }

        public static Optional<TData> Empty()
        {
            return new();
        }
    }
}