// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.YaccParser
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// This class implements lexical scanner for DocumentDB SQL grammar.
    /// Calling public 'Scan' function scans a single token and returns the following information:
    /// Token Id           : The integer id of the token that was scanned\
    /// Token Value        : An integer or double value associated with the scanned token, if applicable
    /// Token Lex Location : The start/end indexes of the token text in the input buffer
    /// The scanner does not do any allocations of any kind. For instance, text tokens wouldn't have a string
    /// value; rather, the returned lexical location could be used to copy the value of the string to a provided
    /// buffer. This keeps the implementation simple and fast and avoids any unnecessary string allocations if
    /// it was not the intention of the caller.
    /// We opted for hand-crafted scanner rather than using LEX generated one so that we have more flexibility
    /// and control over the behavior. It should also serve us better in the future if we need to implement a
    /// line-scanner on top of it (for colorizing/intillisense).
    /// </summary>
    internal sealed class Scanner
    {
        private delegate bool Predicate(char value);

        /// <summary>
        /// Standard definition for yylex
        /// </summary>
#pragma warning disable SA1310 // Field names should not contain underscore
        private const int END_OF_FILE = 0;
#pragma warning restore SA1310 // Field names should not contain underscore

        /// <summary>
        /// Standard definition for yylex
        /// </summary>
#pragma warning disable SA1310 // Field names should not contain underscore
        private const int SCANNER_ERROR = -1;
#pragma warning restore SA1310 // Field names should not contain underscore

        private BufferReader reader;

        public Scanner(ReadOnlyMemory<char> buffer)
        {
            this.reader = new BufferReader(buffer);
        }

        public int Scan(Memory<char> textBuffer, out TokenValue tokenValue, out SqlLocation sqlLocation)
        {
            int tokenId = this.Scan(out tokenValue);
            sqlLocation = new SqlLocation((ulong)this.reader.atomStartIndex, (ulong)this.reader.atomEndIndex);
            return tokenId;
        }

        public int Scan(Memory<char> textBuffer, out TokenValue tokenValue)
        {
            this.reader.StartNewAtom();

            if (!this.reader.TryReadNext(out char current))
            {
                tokenValue = default;
                return END_OF_FILE;
            }

            switch (current)
            {
                case '"':
                case '\'':
                    return ScanQuotedString(current, textBuffer, out tokenValue);

                case ',':
                case ':':
                case '{':
                case '}':
                case '[':
                case ']':
                case '(':
                case ')':
                    tokenValue = TokenValue.FromLong((long)current);
                    return (int)tokenValue.LongValue;

                case '+':
                case '*':
                case '/':
                case '%':
                case '~':
                case '&':
                case '^':
                case '=':
                    tokenValue = TokenValue.FromLong((long)current);
                    return (int)tokenValue.LongValue;

                case '-':
                    if (this.reader.TryReadNextIfEquals('-'))
                    {
                        // "--" (single-line comment)
                        return ScanSingleLineComment(value, pwchTextBuffer, cchTextBuffer);
                    }

                    // "-"
                    tokenValue = TokenValue.FromLong((long)current);
                    return (int)tokenValue.LongValue;

                case '|':
                    if (this.reader.TryReadNextIfEquals('|'))
                    {
                        // "||"
                        tokenValue = TokenValue.FromLong(y_tab._STR_CONCAT);
                    }
                    else
                    {
                        // "|"
                        tokenValue = TokenValue.FromLong((long)current);
                    }
                    return (int)tokenValue.LongValue;

                case '<':
                    if (this.reader.TryReadNextIfEquals('='))
                    {
                        // "<="
                        tokenValue = TokenValue.FromLong(y_tab._LE);
                    }
                    else if (this.reader.TryReadNextIfEquals('>'))
                    {
                        // "<>"
                        tokenValue = TokenValue.FromLong(y_tab._NE);
                    }
                    else if (this.reader.TryReadNextIfEquals('<'))
                    {
                        // "<<"
                        tokenValue = TokenValue.FromLong(y_tab._LSHIFT);
                    }
                    else
                    {
                        // "<"
                        tokenValue = TokenValue.FromLong((long)current);
                    }
                    return (int)tokenValue.LongValue;

                case '>':
                    if (this.reader.TryReadNextIfEquals('='))
                    {
                        // ">="
                        tokenValue = TokenValue.FromLong(y_tab._GE);
                    }
                    else if (this.reader.TryReadNextIfEquals('>'))
                    {
                        if (this.reader.TryReadNextIfEquals('>'))
                        {
                            // ">>>"
                            tokenValue = TokenValue.FromLong(y_tab._RSHIFT_ZF);
                        }
                        else
                        {
                            // ">>"
                            tokenValue = TokenValue.FromLong(y_tab._RSHIFT);
                        }
                    }
                    else
                    {
                        // ">"
                        tokenValue = TokenValue.FromLong((long)current);
                    }
                    return (int)tokenValue.LongValue;

                case '?':
                    if (this.reader.TryReadNextIfEquals('?'))
                    {
                        // "??"
                        tokenValue = TokenValue.FromLong(y_tab._COALESCE);
                    }
                    else
                    {
                        // "?"
                        tokenValue = TokenValue.FromLong((long)current);
                    }
                    return (int)tokenValue.LongValue;

                case '!':
                    if (this.reader.TryReadNextIfEquals('='))
                    {
                        // "!="
                        tokenValue = TokenValue.FromLong(y_tab._NE);
                        return (int)tokenValue.LongValue;
                    }

                    // '!' is not a valid token by itself
                    break;

                case '.':
                    if (this.reader.CheckNext(IsDigit))
                    {
                        // Scan a decimal number
                        this.reader.UndoRead();
                        return ScanDecimal(value);
                    }

                    tokenValue = TokenValue.FromLong((long)current);
                    return (int)tokenValue.LongValue;

                case '@':
                    {
                        char nextChar;
                        if (this.reader.TryReadNextIf(IsIdentifierStart, out nextChar))
                        {
                            return ScanParameter(value, pwchTextBuffer, cchTextBuffer);
                        }

                        // '@' is not a valid token by itself
                        break;
                    }
            }

            // Scan white spaces
            if (IsWhitespace(current))
            {
                this.reader.AdvanceWhile(IsWhitespace);
                return y_tab._LEX_WHITE;
            }

            // Scan an identifier
            if (IsIdentifierStart(current))
            {
                return ScanIdentifier(value, pwchTextBuffer, cchTextBuffer);
            }

            // Scan a number
            if (IsDigit(current))
            {
                // Check whether this is a hex number or decimal
                if ((current == '0') && this.reader.TryReadNextIfEquals('x', 'X'))
                {
                    return ScanHexNumber(value);
                }

                // Undo the digit read and scan as decimal
                this.reader.UndoRead();
                return ScanDecimal(value);
            }

            tokenValue = default;
            return y_tab._LEX_INVALID;
        }

        private static int ScanQuotedString(char quotationChar, Memory<char> textBuffer, out TokenValue value)
        {

        }

        public readonly struct TokenValue
        {
            private readonly Either<long, double> either;

            private TokenValue(Either<long, double> either)
            {
                this.either = either;
            }

            public bool IsLong => this.either.IsLeft;
            public bool IsDouble => this.either.IsRight;

            public long LongValue => this.either.FromLeft(default);

            public double DoubleValue => this.either.FromRight(default);

            public static TokenValue FromLong(long value) => new TokenValue(value);

            public static TokenValue FromDouble(double value) => new TokenValue(value);

            public static implicit operator Either<long, double>(TokenValue value) => value.either;
            public static explicit operator TokenValue(Either<long, double> value) => new TokenValue(value);
            public static explicit operator TokenValue(long value) => new TokenValue(value);
            public static explicit operator TokenValue(double value) => new TokenValue(value);
        }

        private sealed class BufferReader
        {
            private ReadOnlyMemory<char> buffer;
            public int atomStartIndex;
            public int atomEndIndex;

            public BufferReader(ReadOnlyMemory<char> buffer)
            {
                this.buffer = buffer;
            }

            public bool IsEof => this.atomEndIndex >= this.buffer.Length;

            public int AtomLength => this.atomEndIndex - this.atomStartIndex;

            public bool CheckIfNextEquals(char c) => !this.IsEof && (c == this.buffer.Span[this.atomEndIndex]);
            public bool CheckIfNextEquals(char c1, char c2) => this.CheckIfNextEquals(c1) || this.CheckIfNextEquals(c2);
            public bool CheckNext(Predicate predicate) => !this.IsEof && predicate(this.buffer.Span[this.atomEndIndex]);
            public bool TryReadNext(out char value)
            {
                if (!this.IsEof)
                {
                    value = this.buffer.Span[this.atomEndIndex++];
                    return true;
                }

                // We have reached EOF
                value = default;
                return false;
            }

            public bool TryReadNextIf(Predicate predicate, out char c)
            {
                if (predicate == null)
                {
                    throw new ArgumentNullException(nameof(predicate));
                }

                if (!this.IsEof && predicate(this.buffer.Span[this.atomEndIndex]))
                {
                    c = this.buffer.Span[this.atomEndIndex++];
                    return true;
                }

                c = default;
                return false;
            }

            public bool TryReadNextIfEquals(char c)
            {
                if (!this.IsEof && (c == this.buffer.Span[this.atomEndIndex]))
                {
                    this.atomEndIndex++;
                    return true;
                }

                return false;
            }

            public bool TryReadNextIfEquals(char c1, char c2)
            {
                if (!this.IsEof && ((c1 == this.buffer.Span[this.atomEndIndex]) || (c2 == this.buffer.Span[this.atomEndIndex])))
                {
                    this.atomEndIndex++;
                    return true;
                }

                return false;
            }

            public int ReadNextDigit()
            {
                int value = -1;

                if (!this.IsEof)
                {
                    value = GetDigitValue(this.buffer.Span[this.atomEndIndex]);

                    // Commit the read if it is a valid digit
                    if (value >= 0)
                    {
                        this.atomEndIndex++;
                    }
                }

                Debug.Assert((value >= -1) && (value <= 9));

                return value;
            }

            public int ReadNextHexDigit()
            {
                int value = -1;

                if (!this.IsEof)
                {
                    value = GetHexDigitValue(this.buffer.Span[this.atomEndIndex++]);

                    // Commit the read if it is a valid hex digit
                    if (value >= 0)
                    {
                        this.atomEndIndex++;
                    }
                }

                Debug.Assert((value >= -1) && (value <= 15));

                return value;
            }

            public int AdvanceWhile(Predicate predicate, bool condition)
            {
                Debug.Assert(predicate != null);

                int nStartIndex = this.atomEndIndex;

                while (!this.IsEof)
                {
                    if (predicate(this.buffer.Span[this.atomEndIndex]) != condition)
                    {
                        break;
                    }

                    this.atomEndIndex++;
                }

                return this.atomEndIndex - nStartIndex;
            }

            public bool TryUndoRead()
            {
                // We can only undo up to the start index of the atom
                if (this.atomEndIndex > this.atomStartIndex)
                {
                    this.atomEndIndex--;
                    return true;
                }

                Debug.Assert(this.atomStartIndex == this.atomEndIndex);
                return false;
            }

            public void StartNewAtom()
            {
                this.atomStartIndex = this.atomEndIndex;
            }

            public void ResetCurrentAtom()
            {
                this.atomEndIndex = this.atomStartIndex;
            }

            public bool TryParseAtomAsDecimal(out double value)
            {
                return double.TryParse(this.buffer.Slice(this.atomStartIndex, this.AtomLength).ToString(), out value);
            }

            public bool TryCopyAtomText(Memory<char> destination)
            {
                if (destination.Length < this.AtomLength)
                {
                    return false;
                }

                this.buffer.CopyTo(destination);
                return true;
            }

            public StringToken RetrieveAtomStringToken()
            {
                return StringTokenLookup.Find(this.buffer.Slice(this.atomStartIndex, this.AtomLength).Span);
            }

            private static int GetDigitValue(char c)
            {
                if ((c >= '0') && (c <= '9')) return c - '0';

                // not a digit
                return -1;
            }

            private static int GetHexDigitValue(char c)
            {
                if ((c >= '0') && (c <= '9')) return c - '0';
                if ((c >= 'a') && (c <= 'f')) return c - 'a' + 10;
                if ((c >= 'A') && (c <= 'F')) return c - 'A' + 10;

                // not a hex digit
                return -1;
            }
        }
    }
}
