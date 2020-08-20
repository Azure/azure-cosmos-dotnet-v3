//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Text;
    using Newtonsoft.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class UtfAllString : IEquatable<UtfAllString>
    {
        private UtfAllString(
            Utf8Memory utf8String,
            string utf16String,
            Utf8Memory utf8EscapedString,
            string utf16EscapedString)
        {
            this.Utf8String = utf8String;
            this.Utf16String = utf16String;
            this.Utf8EscapedString = utf8EscapedString;
            this.Utf16EscapedString = utf16EscapedString;
        }

        public Utf8Memory Utf8String { get; }

        public string Utf16String { get; }

        public Utf8Memory Utf8EscapedString { get; }

        public string Utf16EscapedString { get; }

        public static UtfAllString Create(string utf16String)
        {
            if (utf16String == null)
            {
                throw new ArgumentNullException(nameof(utf16String));
            }

            Utf8Memory utf8String = Utf8Memory.UnsafeCreateNoValidation(Encoding.UTF8.GetBytes(utf16String));

            string utf16EscapedString = JsonConvert.ToString(utf16String);
            utf16EscapedString = utf16EscapedString.Substring(1, utf16EscapedString.Length - 2);

            Utf8Memory utf8EscapedString = Utf8Memory.UnsafeCreateNoValidation(Encoding.UTF8.GetBytes(utf16EscapedString));

            return new UtfAllString(utf8String, utf16String, utf8EscapedString, utf16EscapedString);
        }

        public static UtfAllString Create(Utf8Memory utf8String)
        {
            string utf16String = utf8String.ToString();

            string utf16EscapedString = JsonConvert.ToString(utf16String);
            utf16EscapedString = utf16EscapedString.Substring(1, utf16EscapedString.Length - 2);

            Utf8Memory utf8EscapedString = Utf8Memory.UnsafeCreateNoValidation(Encoding.UTF8.GetBytes(utf16EscapedString));

            return new UtfAllString(utf8String, utf16String, utf8EscapedString, utf16EscapedString);
        }

        public bool Equals(UtfAllString other)
        {
            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null)
            {
                return false;
            }

            return this.Utf8String.Equals(other.Utf8String);
        }
    }
}
