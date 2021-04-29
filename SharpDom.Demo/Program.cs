using System;
using System.IO;
using SharpDom.Tokenization;

namespace SharpDom.Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            var tokenizer = new HtmlTokenizer(File.ReadAllText("simple.html"), true);
            
            Console.WriteLine("=== <Begin Tokenization> ===");
            
            var tokenizationResult = tokenizer.Run();
            var tokens = tokenizationResult.Tokens;
            var errors = tokenizationResult.Errors;

            Console.WriteLine("=== <End Tokenization> ===");
            Console.WriteLine();
            Console.WriteLine("=== <Begin Tokens> ===");
            
            foreach (var token in tokens)
            {
                Console.WriteLine($"{token}");
            }
            
            Console.WriteLine("=== <End Tokens> ===");
            Console.WriteLine();
            Console.WriteLine("=== <Begin Errors> ===");
            
            foreach (var error in errors)
            {
                Console.WriteLine($"{error}");
            }
            
            Console.WriteLine("=== <End Errors> ===");

        }
    }
}