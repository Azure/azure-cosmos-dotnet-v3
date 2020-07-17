// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#pragma warning disable SA1309 // Field names should not begin with underscore
namespace Microsoft.Azure.Cosmos.Query.Core.HandwrittenParser
{
    internal static class CharCodes
    {
        public const char OpenParen = (char)0x28;
        public const char CloseParen = (char)0x29;
        public const char OpenBracket = (char)0x5b;
        public const char CloseBracket = (char)0x5d;
        public const char OpenBrace = (char)0x7b;
        public const char CloseBrace = (char)0x7d;
        public const char A = (char)0x41;
        public const char E = (char)0x45;
        public const char Z = (char)0x5a;
        public const char a = (char)0x61;
        public const char e = (char)0x65;
        public const char z = (char)0x7a;
        public const char _0 = (char)0x30;
        public const char _1 = (char)0x31;
        public const char _2 = (char)0x32;
        public const char _3 = (char)0x33;
        public const char _4 = (char)0x34;
        public const char _5 = (char)0x35;
        public const char _6 = (char)0x36;
        public const char _7 = (char)0x37;
        public const char _8 = (char)0x38;
        public const char _9 = (char)0x39;
        public const char LineFeed = (char)0x0a; // \n
        public const char CarriageReturn = (char)0x0d; // \r
        public const char Space = (char)0x20;
        public const char Tab = (char)0x09;
        public const char Asterisk = (char)0x2a;
        public const char Plus = (char)0x2b;
        public const char Minus = (char)0x2d;
        public const char Ampersand = (char)0x26;
        public const char Bar = (char)0x7c;
        public const char Caret = (char)0x5e;
        public const char Slash = (char)0x2f;
        public const char Backslash = (char)0x5c;
        public const char EqualSign = (char)0x3d;
        public const char GreaterThan = (char)0x3e;
        public const char LessThan = (char)0x3c;
        public const char Percent = (char)0x25;
        public const char Bang = (char)0x21;
        public const char Tilde = (char)0x7e;
        public const char SingleQuote = (char)0x27;
        public const char DoubleQuote = (char)0x22;
        public const char Dot = (char)0x2e;
        public const char Comma = (char)0x2c;
        public const char Question = (char)0x3f;
        public const char Colon = (char)0x3a;
    }
}
