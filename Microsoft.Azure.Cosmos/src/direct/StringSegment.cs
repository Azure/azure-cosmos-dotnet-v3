//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
     
    /// <summary>
    /// Wrapper around a string that provides alloc-free methods for
    /// SubString, Trim, and comparisons.
    /// </summary>
    /// <remarks>
    /// This is used over the standard .net ReadOnlyMemory{char} or StringSegment
    /// as Documents.Common's and Document.Client references to System.Buffers was deemed to be too expensive
    /// to add and maintain. if these references get fixed, this struct can be deleted in favor of the one from the
    /// BCL.
    /// </remarks>
    internal readonly struct StringSegment
    {
        private readonly string value;

        public StringSegment(string value)
        {
            this.value = value;
            this.Start = 0;
            this.Length = value?.Length ?? 0;
        }

        public StringSegment(string value, int start, int length)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (start < 0 || (start >= value.Length && value.Length > 0))
            {
                throw new ArgumentException(nameof(start));
            }

            if (length < 0 || (start + length) > value.Length)
            {
                throw new ArgumentException(nameof(length));
            }

            this.value = value;
            this.Start = start;
            this.Length = length;
        }

        private int Start { get; }

        public int Length { get; }
        
        public static implicit operator StringSegment(string b) => new StringSegment(b);

        public bool IsNullOrEmpty()
        {
            return string.IsNullOrEmpty(this.value) || this.Length == 0;
        }
        public int Compare(string other, StringComparison comparison)
        {
            return string.Compare(this.value, this.Start, other, 0, Math.Max(this.Length, other.Length), comparison);
        }

        public int Compare(StringSegment other, StringComparison comparison)
        {
            return string.Compare(this.value, this.Start, other.value, other.Start, Math.Max(this.Length, other.Length), comparison);
        }

        public bool Equals(string other, StringComparison comparison)
        {
            return this.Compare(other, comparison) == 0;
        }

        public StringSegment Substring(int start, int length)
        {
            if (length == 0)
            {
                return new StringSegment(string.Empty);
            }

            if (start > this.Length)
            {
                throw new ArgumentException(nameof(start));
            }

            if ((start + length) > this.Length)
            {
                throw new ArgumentException(nameof(length));
            }

            return new StringSegment(this.value, start + this.Start, length);
        }

        public int LastIndexOf(char segment)
        {
            if (this.IsNullOrEmpty())
            {
                return -1;
            }

            int index = this.value.LastIndexOf(segment, (this.Start + this.Length - 1));
            if (index >= 0)
            {
                return index - this.Start;
            }

            return index;
        }
        public StringSegment TrimStart(char[] trimChars)
        {
            if (this.Length == 0)
            {
                return new StringSegment(string.Empty, 0, 0);
            }

            int newStart = this.Start;
            int newLength = this.Length;
            while (newLength > 0 && this.value.IndexOfAny(trimChars, newStart, 1) == newStart)
            {
                newStart++;
                newLength--;
            }

            return new StringSegment(this.value, newStart, newLength);
        }

        public StringSegment TrimEnd(char[] trimChars)
        {
            if (this.Length == 0)
            {
                return new StringSegment(string.Empty, 0, 0);
            }

            int newLength = this.Length;
            int newEndIndex = this.Start + this.Length - 1;
            while (newLength > 0 && this.value.LastIndexOfAny(trimChars, newEndIndex, 1) == newEndIndex)
            {
                newLength--;
                newEndIndex--;
            }

            return new StringSegment(this.value, this.Start, newLength);
        }

        public string GetString()
        {
            if (this.Length == 0)
            {
                return string.Empty;
            }

            if (this.Length == this.value.Length)
            {
                return this.value;
            }

            return this.value.Substring(this.Start, this.Length);
        }
    }
}
