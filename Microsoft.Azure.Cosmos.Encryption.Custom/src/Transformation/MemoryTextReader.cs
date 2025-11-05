//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;

    /// <summary>
    /// Adjusted implementation of .Net StringReader reading from a Memory{char} instead of a string.
    /// </summary>
    internal class MemoryTextReader : TextReader
    {
        private Memory<char> chars;
        private int length;
        private int pos;
        private bool closed;

        public MemoryTextReader(Memory<char> chars)
        {
            this.chars = chars;
            this.length = chars.Length;
        }

        public override void Close()
        {
            this.Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            this.chars = null;
            this.pos = 0;
            this.length = 0;
            this.closed = true;
            base.Dispose(disposing);
        }

        [Pure]
        public override int Peek()
        {
            if (this.closed)
            {
                throw new InvalidOperationException("Reader is closed");
            }

            if (this.pos == this.length)
            {
                return -1;
            }

            return this.chars.Span[this.pos];
        }

        public override int Read()
        {
            if (this.closed)
            {
                throw new InvalidOperationException("Reader is closed");
            }

            if (this.pos == this.length)
            {
                return -1;
            }

            return this.chars.Span[this.pos++];
        }

        public override int Read(char[] buffer, int index, int count)
        {
            ArgumentValidation.ThrowIfNull(buffer);
            ArgumentValidation.ThrowIfNegative(index);
            ArgumentValidation.ThrowIfNegative(count);
            ArgumentValidation.ThrowIfGreaterThan(count, buffer.Length - index);

            if (this.closed)
            {
                throw new InvalidOperationException("Reader is closed");
            }

            int n = this.length - this.pos;
            if (n > 0)
            {
                if (n > count)
                {
                    n = count;
                }

                this.chars.Span.Slice(this.pos, n).CopyTo(buffer.AsSpan(index, n));
                this.pos += n;
            }

            return n;
        }

        public override string ReadToEnd()
        {
            if (this.closed)
            {
                throw new InvalidOperationException("Reader is closed");
            }

            this.pos = this.length;
            Memory<char> remaining = this.chars.Slice(this.pos, this.length - this.pos);
            return new string(remaining.ToArray());
        }

        public override string ReadLine()
        {
            if (this.closed)
            {
                throw new InvalidOperationException("Reader is closed");
            }

            int i = this.pos;
            while (i < this.length)
            {
                char ch = this.chars.Span[i];
                if (ch == '\r' || ch == '\n')
                {
                    string result = new (this.chars.Slice(this.pos, i - this.pos).ToArray());
                    this.pos = i + 1;
                    if (ch == '\r' && this.pos < this.length && this.chars.Span[this.pos] == '\n')
                    {
                        this.pos++;
                    }

                    return result;
                }

                i++;
            }

            if (i > this.pos)
            {
                string result = new (this.chars.Slice(this.pos, i - this.pos).ToArray());
                this.pos = i;
                return result;
            }

            return null;
        }
    }
}
