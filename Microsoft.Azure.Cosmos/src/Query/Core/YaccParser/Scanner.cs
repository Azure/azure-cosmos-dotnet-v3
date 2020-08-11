// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.YaccParser
{
    using System;
    using System.Diagnostics;
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

        public int Scan(Memory<char> textBuffer, out TokenValue tokevalue, out SqlLocation sqlLocation)
        {
            int tokenId = this.Scan(textBuffer, out tokevalue);
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
                    return this.ScanQuotedString(current, textBuffer, out tokenValue);

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
                        return this.ScanSingleLineComment(textBuffer, out tokenValue);
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
                    if (this.reader.CheckNext(Scanner.IsDigit))
                    {
                        // Scan a decimal number
                        this.reader.TryUndoRead();
                        return this.ScanDecimal(out tokenValue);
                    }

                    tokenValue = TokenValue.FromLong((long)current);
                    return (int)tokenValue.LongValue;

                case '@':
                    {
                        if (this.reader.TryReadNextIf(IsIdentifierStart, out char nextChar))
                        {
                            return this.ScanParameter(textBuffer, out tokenValue);
                        }

                        // '@' is not a valid token by itself
                        break;
                    }
            }

            // Scan white spaces
            if (IsWhitespace(current))
            {
                this.reader.AdvanceWhile(IsWhitespace);
                tokenValue = default;
                return y_tab._LEX_WHITE;
            }

            // Scan an identifier
            if (IsIdentifierStart(current))
            {
                return this.ScanIdentifier(textBuffer, out tokenValue);
            }

            // Scan a number
            if (IsDigit(current))
            {
                // Check whether this is a hex number or decimal
                if ((current == '0') && this.reader.TryReadNextIfEquals('x', 'X'))
                {
                    return this.ScanHexNumber(out tokenValue);
                }

                // Undo the digit read and scan as decimal
                this.reader.TryUndoRead();
                return this.ScanDecimal(out tokenValue);
            }

            tokenValue = default;
            return y_tab._LEX_INVALID;
        }

        private enum ScanQuotedStringState
        {
            Continue,
            ReadEscapedCharacter,
            ReadUnicodeCharacter,
            Done,
            Error
        }

        private readonly struct UnicodeCharacter
        {
            public UnicodeCharacter(char value, ushort digits)
            {
                this.Value = value;
                this.Digits = digits;
            }

            public char Value { get; }
            public ushort Digits { get; }
        }

        private int ScanQuotedString(char quotationChar, Memory<char> textBuffer, out TokenValue value)
        {
            Debug.Assert((quotationChar == '"') || (quotationChar == '\''));
            Debug.Assert(this.reader.AtomLength == 1);

            const char EscapeChar = '\\';
            int length = 0;
            UnicodeCharacter unicodeChar = default;
            ScanQuotedStringState state = ScanQuotedStringState.Continue;

            char current;
            while (this.reader.TryReadNext(out current))
            {
                switch (state)
                {
                    case ScanQuotedStringState.Continue:
                        if (current == quotationChar)
                        {
                            // We got to the end of the string
                            state = ScanQuotedStringState.Done;
                        }
                        else if (current == EscapeChar)
                        {
                            state = ScanQuotedStringState.ReadEscapedCharacter;
                        }

                        break;

                    case ScanQuotedStringState.ReadEscapedCharacter:
                        if (current == 'u')
                        {
                            // reset Unicode sequence
                            unicodeChar = default;
                            state = ScanQuotedStringState.ReadUnicodeCharacter;
                        }
                        else
                        {
                            switch (current)
                            {
                                case 'b':
                                    current = '\b';
                                    break;
                                case 'f':
                                    current = '\f';
                                    break;
                                case 'n':
                                    current = '\n';
                                    break;
                                case 'r':
                                    current = '\r';
                                    break;
                                case 't':
                                    current = '\t';
                                    break;
                                default:
                                    // Nothing to do for \\, \/ and \"
                                    break;
                            }

                            // set the state to Normal so that the character is accepted
                            state = ScanQuotedStringState.Continue;
                        }

                        break;

                    case ScanQuotedStringState.ReadUnicodeCharacter:
                        if (IsHexadecimal(current))
                        {
                            Debug.Assert(unicodeChar.Digits < 4);

                            unicodeChar = new UnicodeCharacter(
                                value: (char)((unicodeChar.Value << 4) + GetHexDigitValue(current)),
                                digits: (ushort)(unicodeChar.Digits + 1));

                            if (unicodeChar.Digits == 4)
                            {
                                current = unicodeChar.Value;
                                state = ScanQuotedStringState.Continue;
                            }
                        }
                        else
                        {
                            // Not a valid Unicode sequence after "\u"
                            state = ScanQuotedStringState.Error;
                        }

                        break;

                    default:
                        // Unreachable
                        Debug.Assert(false);
                        break;
                }

                // Check the state at the end of each loop to determine whether to continue
                // scanning or quit the loop if we are done or ecountered an error
                if (state == ScanQuotedStringState.Continue)
                {
                    // copy the current character to the output buffer
                    if (textBuffer.Length > length)
                    {
                        textBuffer.Span[length] = current;
                    }

                    // accept the character and continue
                    length++;
                }
                else if ((state == ScanQuotedStringState.Done) || (state == ScanQuotedStringState.Error))
                {
                    // exit the loop
                    break;
                }
            }

            // Check if we ecountered the EOF before the closing quotation character
            if (state != ScanQuotedStringState.Done)
            {
                state = ScanQuotedStringState.Error;
            }

            Debug.Assert((state == ScanQuotedStringState.Done) || (state == ScanQuotedStringState.Error));
            Debug.Assert((state != ScanQuotedStringState.Done) || (current == quotationChar));

            // For text tokens, we set the integer value to the text length
            value = TokenValue.FromLong(length);

            return state == ScanQuotedStringState.Done ? y_tab._STRING : y_tab._LEX_INVALID_STRING;
        }

        private int ScanSingleLineComment(Memory<char> textBuffer, out TokenValue tokevalue)
        {
            Debug.Assert(this.reader.AtomLength == 2);

            // Advance until the end of line
            this.reader.AdvanceWhile(IsEol, false);

            // Copy the text to the out buffer
            this.reader.TryCopyAtomText(textBuffer);

            // For text tokens, we set the integer value to the text length
            tokevalue = TokenValue.FromLong(this.reader.AtomLength);

            return y_tab._LEX_COMMENT;
        }

        private enum ScanDecimalState
        {
            Accept,
            Parse,
            Error
        }

        private int ScanDecimal(out TokenValue tokenValue)
        {
            const ulong AbsIntegerMinValue = 1UL << 63;
            Debug.Assert(this.reader.AtomLength == 0);
            ulong value = 0;

            ulong digit = 0;
            ulong count = 0;

            // Scan until we get to non-digit character
            while ((digit = (ulong)this.reader.ReadNextDigit()) >= 0)
            {
                Debug.Assert((digit >= 0) && (digit <= 9));

                // need to skip leading zeros
                if ((count > 0) || (digit > 0))
                {
                    count++;

                    value *= 10;
                    value += digit;
                }
            }

            ScanDecimalState state = ScanDecimalState.Accept;

            // Check if the 64-bit value has overflowed
            if ((count > 19) || (value > AbsIntegerMinValue))
            {
                state = ScanDecimalState.Parse;
            }

            // If the next character is a '.' then we continue scanning
            if (this.reader.TryReadNextIfEquals('.'))
            {
                // Scan optional digits after the dot
                this.reader.AdvanceWhile(IsDigit);

                // We'll need to call the system function to parse the string
                state = ScanDecimalState.Parse;
            }

            // If the next character is either 'e' or 'E' then we continue
            if (this.reader.TryReadNextIfEquals('e', 'E'))
            {
                // Read optional '+' or '-'
                this.reader.TryReadNextIfEquals('+', '-');

                // We need to have at least a single digit
                if (this.reader.AdvanceWhile(IsDigit) > 0)
                {
                    // We'll need to call the system function to parse the string
                    state = ScanDecimalState.Parse;
                }
                else
                {
                    // Invalid number
                    state = ScanDecimalState.Error;
                }
            }

            int tokenId = 0;

            // Check if we need to parse the atom text
            if (state == ScanDecimalState.Parse)
            {
                if (this.reader.TryParseAtomAsDecimal(out double doubleValue))
                {
                    // Check for overflow (i.e. infinity)
                    if (double.IsNaN(doubleValue))
                    {
                        doubleValue = 0.0;
                        state = ScanDecimalState.Error;
                    }
                    else
                    {
                        state = ScanDecimalState.Accept;
                    }

                    tokenId = y_tab._DOUBLE;
                    tokenValue = TokenValue.FromDouble(doubleValue);
                }
                else
                {
                    state = ScanDecimalState.Error;
                }
            }
            else if (state == ScanDecimalState.Accept)
            {
                if (value == AbsIntegerMinValue)
                {
                    tokenId = y_tab._INTEGER_ABS_MIN_VALUE;
                    tokenValue = TokenValue.FromDouble(AbsIntegerMinValue);
                }
                else
                {
                    tokenId = y_tab._INTEGER;
                    tokenValue = TokenValue.FromLong((long)value);
                }
            }

            Debug.Assert((state == ScanDecimalState.Accept) || (state == ScanDecimalState.Error));

            // Right after the number there should not be an identifier character. This is basically
            // to disallow a value like "1234abc" to be tokenized as _DOUBLE(123) followed by _ID(abc);
            // rather, we need to tokenize it as a single token _INVALID(1234abc). This applies 
            // regardless whether we are in Accept or Error state.
            if (this.reader.AdvanceWhile(IsIdentifierCharacter) > 0)
            {
                state = ScanDecimalState.Error;
            }

            tokenValue = default;
            return state == ScanDecimalState.Accept ? tokenId : y_tab._LEX_INVALID_DOUBLE;
        }

        private int ScanParameter(Memory<char> textBuffer, out TokenValue tokenValue)
        {
            Debug.Assert(this.reader.AtomLength == 2);

            // Scan until we get to non-identifier character
            this.reader.AdvanceWhile(IsIdentifierCharacter);

            // Copy the atom text to the provided buffer and set the token integer.
            // value to the actual length of the scanned atom.
            // The copied text includes leading '@' character.
            this.reader.TryCopyAtomText(textBuffer);
            tokenValue = TokenValue.FromLong(this.reader.AtomLength);

            return y_tab._PARAMETER;
        }

        private int ScanIdentifier(Memory<char> textBuffer, out TokenValue tokenValue)
        {
            Debug.Assert(this.reader.AtomLength == 1);

            // Scan until we get to non-identifier character
            this.reader.AdvanceWhile(IsIdentifierCharacter);

            int nTokenId = 0;
            StringToken stringToken = this.reader.RetrieveAtomStringToken();
            switch (stringToken)
            {
                // Grammar Keywords
                case StringToken.And: nTokenId = y_tab._AND; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Array: nTokenId = y_tab._ARRAY; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.As: nTokenId = y_tab._AS; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Asc: nTokenId = y_tab._ASC; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Between: nTokenId = y_tab._BETWEEN; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.By: nTokenId = y_tab._BY; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Case: nTokenId = y_tab._CASE; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Cast: nTokenId = y_tab._CAST; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Convert: nTokenId = y_tab._CONVERT; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Cross: nTokenId = y_tab._CROSS; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Distinct: nTokenId = y_tab._DISTINCT; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Desc: nTokenId = y_tab._DESC; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Else: nTokenId = y_tab._ELSE; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.End: nTokenId = y_tab._END; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Exists: nTokenId = y_tab._EXISTS; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Escape: nTokenId = y_tab._ESCAPE; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.False: nTokenId = y_tab._FALSE; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.For: nTokenId = y_tab._FOR; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.From: nTokenId = y_tab._FROM; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Group: nTokenId = y_tab._GROUP; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Having: nTokenId = y_tab._HAVING; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.In: nTokenId = y_tab._IN; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Inner: nTokenId = y_tab._INNER; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Insert: nTokenId = y_tab._INSERT; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Into: nTokenId = y_tab._INTO; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Is: nTokenId = y_tab._IS; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Join: nTokenId = y_tab._JOIN; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Left: nTokenId = y_tab._LEFT; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Like: nTokenId = y_tab._LIKE; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Limit: nTokenId = y_tab._LIMIT; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Not: nTokenId = y_tab._NOT; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Null: nTokenId = y_tab._NULL; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Offset: nTokenId = y_tab._OFFSET; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.On: nTokenId = y_tab._ON; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Or: nTokenId = y_tab._OR; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Order: nTokenId = y_tab._ORDER; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Outer: nTokenId = y_tab._OUTER; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Over: nTokenId = y_tab._OVER; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Right: nTokenId = y_tab._RIGHT; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Select: nTokenId = y_tab._SELECT; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Set: nTokenId = y_tab._SET; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Then: nTokenId = y_tab._THEN; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Top: nTokenId = y_tab._TOP; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.True: nTokenId = y_tab._TRUE; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Udf: nTokenId = y_tab._UDF; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Undefined: nTokenId = y_tab._UNDEFINED; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Update: nTokenId = y_tab._UPDATE; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Value: nTokenId = y_tab._VALUE; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.When: nTokenId = y_tab._WHEN; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.Where: nTokenId = y_tab._WHERE; tokenValue = TokenValue.FromLong(nTokenId); break;
                case StringToken.With: nTokenId = y_tab._WITH; tokenValue = TokenValue.FromLong(nTokenId); break;

                // Double special values
                case StringToken.NaN:
                    nTokenId = y_tab._DOUBLE;
                    tokenValue = TokenValue.FromDouble(double.NaN);
                    break;
                case StringToken.Infinity:
                    nTokenId = y_tab._DOUBLE;
                    tokenValue = TokenValue.FromDouble(double.PositiveInfinity);
                    break;

                // Unrecognized or non-keyword tokens
                case StringToken.NONE:
                default:
                    // Since the token is not recognized, we scan it as an identifier.
                    // We copy the atom text to the provided buffer and set the token integer 
                    // value to the actual length of the scanned atom.
                    nTokenId = y_tab._ID;
                    this.reader.TryCopyAtomText(textBuffer);
                    tokenValue = TokenValue.FromLong(this.reader.AtomLength);
                    break;
            }

            return nTokenId;
        }

        private static bool IsDigit(char c) => (c >= '0') && (c <= '9');
        private static bool IsEol(char c) => (c == '\r') || (c == '\n');

        private static bool IsHexadecimal(char c) => (c >= '0' && c <= '9') ||
            (c >= 'A' && c <= 'F') ||
            (c >= 'a' && c <= 'f');

        private static bool IsIdentifierStart(char c) => (c == '_') || char.IsLetter(c);

        private static bool IsIdentifierCharacter(char c) => IsIdentifierStart(c);

        private static bool IsWhitespace(char c) => char.IsWhiteSpace(c);

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

        private int ScanHexNumber(out TokenValue tokenValue)
        {
            Debug.Assert(this.reader.AtomLength == 2);

            ulong number = 0;
            bool isValid = false;

            ulong digit = 0;
            ulong count = 0;

            while ((digit = (ulong)this.reader.ReadNextHexDigit()) >= 0)
            {
                Debug.Assert((digit >= 0) && (digit <= 15));

                // a valid hexadecimal number must have at lease one digit
                isValid = true;

                // need to skip leading zeros
                if ((number > 0) || (digit > 0))
                {
                    count++;

                    number <<= 4;
                    number += digit;
                }
            }

            // We need to check for value overflow (i.e. > 64-bit value)
            isValid = isValid && (count <= 16);

            // Right after the number there should not be an identifier character. This is basically
            // to disallow a value like "0x100xyz" to be tokenized as _DOUBLE(255) followed by _ID(xyz);
            // rather, we need to tokenize it as a single token _INVALID(0x100xyz). This applies 
            // regardless whether or not the value is valid.
            if (this.reader.AdvanceWhile(IsIdentifierCharacter) > 0)
            {
                isValid = false;
            }

            tokenValue = TokenValue.FromLong((long)number);
            return isValid ? y_tab._INTEGER : y_tab._LEX_INVALID_DOUBLE;
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
            public delegate bool Predicate(char value);

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

            public int AdvanceWhile(Predicate predicate, bool condition = true)
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
