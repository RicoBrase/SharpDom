using System;

namespace SharpDom.Utils
{
    public class UnreachableException : Exception
    {
        public UnreachableException() : base("This code should not be reachable.")
        {
        }
    }
}