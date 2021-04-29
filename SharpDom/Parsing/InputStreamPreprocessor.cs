using System.Collections.Generic;

namespace SharpDom.Parsing
{
    public class InputStreamPreprocessor
    {

        public static List<byte> PreprocessInputStream(IEnumerable<byte> inputStream)
        {
            var preprocessedInputStream = new List<byte>(inputStream);

            // TODO: Throw parse errors (https://html.spec.whatwg.org/multipage/parsing.html#preprocessing-the-input-stream)
            
            return preprocessedInputStream;
        }

        public static string PreprocessInputStream(string inputString)
        {
            return inputString.Replace("\r", "");
        }
        
    }
}