// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.YaccParser
{
    using System;
    using System.Diagnostics;

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

        public readonly struct TokenValue
        {
            public long LongValue { get; }
            public double DoubleValue { get; }
        }

        private sealed class BufferReader
        {
            private ReadOnlyMemory<char> buffer;
            private int atomStartIndex;
            private int atomEndIndex;

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
                    value = this.GetHexDigitValue(this.buffer.Span[this.atomEndIndex++]);

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
