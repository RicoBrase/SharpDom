using System;

namespace SharpDom.Infra.Unicode
{
    public class Codepoint
    {
        private int _codepoint { get; }

        public Codepoint(int codepoint)
        {
            _codepoint = codepoint;
        }

        public bool IsSurrogate()
        {
            return _codepoint is >= 0xD800 and <= 0xDFFF;
        }

        public bool IsScalar()
        {
            return !IsSurrogate();
        }

        public bool IsNonCharacter()
        {
            return _codepoint is >= 0xFDD0 and <= 0xFDEF
                or 0xFFFE
                or 0xFFFF
                or 0x1FFFE
                or 0x1FFFF
                or 0x2FFFE
                or 0x2FFFF
                or 0x3FFFE
                or 0x3FFFF
                or 0x4FFFE
                or 0x4FFFF
                or 0x5FFFE
                or 0x5FFFF
                or 0x6FFFE
                or 0x6FFFF
                or 0x7FFFE
                or 0x7FFFF
                or 0x8FFFE
                or 0x8FFFF
                or 0x9FFFE
                or 0x9FFFF
                or 0xAFFFE
                or 0xAFFFF
                or 0xBFFFE
                or 0xBFFFF
                or 0xCFFFE
                or 0xCFFFF
                or 0xDFFFE
                or 0xDFFFF
                or 0xEFFFE
                or 0xEFFFF
                or 0xFFFFE
                or 0xFFFFF
                or 0x10FFFE
                or 0x10FFFF;
        }

        public bool IsAscii()
        {
            return _codepoint is >= NULL and <= DELETE;
        }

        public bool IsAsciiTabOrNewline()
        {
            return _codepoint is TAB or LF or CR;
        }

        public bool IsAsciiWhitespace()
        {
            return IsAsciiTabOrNewline() || _codepoint is FF or SPACE;
        }

        public bool IsC0Control()
        {
            return _codepoint is >= NULL and <= INFORMATION_SEPARATOR_ONE;
        }

        public bool IsC0ControlOrSpace()
        {
            return IsC0Control() || _codepoint is SPACE;
        }

        public bool IsControl()
        {
            return IsC0Control() || _codepoint is >= DELETE and <= APPLICATION_PROGRAM_COMMAND;
        }

        public bool IsAsciiDigit()
        {
            return _codepoint is >= 0x0030 and <=0x0039;
        }

        public bool IsAsciiUpperHexDigit()
        {
            return IsAsciiDigit() || _codepoint is >= 0x0041 and <= 0x0046;
        }
        
        public bool IsAsciiLowerHexDigit()
        {
            return IsAsciiDigit() || _codepoint is >= 0x0061 and <= 0x0066;
        }

        public bool IsAsciiHexDigit()
        {
            return IsAsciiUpperHexDigit() || IsAsciiLowerHexDigit();
        }

        public bool IsAsciiUpperAlpha()
        {
            return _codepoint is >= 0x0041 and <= 0x005A;
        }
        
        public bool IsAsciiLowerAlpha()
        {
            return _codepoint is >= 0x0061 and <= 0x007A;
        }

        public bool IsAsciiAlpha()
        {
            return IsAsciiUpperAlpha() || IsAsciiLowerAlpha();
        }

        public bool IsAsciiAlphanumeric()
        {
            return IsAsciiDigit() || IsAsciiAlpha();
        }

        public int Subtract(int subtrahend)
        {
            return _codepoint - subtrahend;
        }
        
        // Static

        public static Codepoint Get(int cp)
        {
            return new(cp);
        }

        public static Codepoint Get(char c)
        {
            return new(c);
        }

        // public static Codepoint FromString(string input)
        // {
        //     if (input == null) throw new ArgumentNullException(nameof(input));
        //     switch (input.Length)
        //     {
        //         case 0:
        //             throw new ArgumentException("String length minimum 1 char.", nameof(input));
        //         case > 2:
        //             throw new ArgumentException("String length maximum 2 chars.", nameof(input));
        //         case 1:
        //             if (int.TryParse(input, out var cp))
        //             {
        //                 return Get(cp);
        //             }
        //             break;
        //         case 2:
        //             if (int.TryParse(input[0].ToString(), out var cp1) && int.TryParse(input[1].ToString(), out var cp2))
        //             {
        //                 return Get();
        //             }
        //             break;
        //     }
        // }
        
        public const char NULL = (char)0x0000;
        public const char TAB = (char)0x0009;
        public const char LF = (char)0x000A;
        public const char FF = (char)0x000C;
        public const char CR = (char)0x000D;
        public const char INFORMATION_SEPARATOR_ONE = (char)0x001F;
        public const char SPACE = (char)0x0020;
        public const char APOSTROPHE = (char) 0x0027;
        public const char GRAVE_ACCENT = (char) 0x0060;
        public const char DELETE = (char)0x007F;
        public const char APPLICATION_PROGRAM_COMMAND = (char)0x009F;
        public const char REPLACEMENT_CHARACTER = (char) 0xFFFD;

    }
}