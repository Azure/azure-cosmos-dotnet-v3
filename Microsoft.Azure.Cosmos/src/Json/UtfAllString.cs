//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Text;

    internal readonly struct UtfAllString
    {
        private UtfAllString(Utf8Memory utf8String, string utf16String)
        {
            this.Utf8String = utf8String;
            this.Utf16String = utf16String;
        }

        public Utf8Memory Utf8String { get; }

        public string Utf16String { get; }

        public static UtfAllString Create(string utf16String)
        {
            if (utf16String == null)
            {
                throw new ArgumentNullException(nameof(utf16String));
            }

            Utf8Memory utf8String = Utf8Memory.UnsafeCreateNoValidation(Encoding.UTF8.GetBytes(utf16String));

            return new UtfAllString(utf8String, utf16String);
        }

        public static UtfAllString Create(Utf8Memory utf8String)
        {
            string utf16String = utf8String.ToString();

            return new UtfAllString(utf8String, utf16String);
        }
    }
}
