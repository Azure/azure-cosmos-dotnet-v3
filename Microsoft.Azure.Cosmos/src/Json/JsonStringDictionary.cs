// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Collections;
    using Microsoft.Azure.Cosmos.Core.Utf8;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class JsonStringDictionary : IJsonStringDictionary, IEquatable<JsonStringDictionary>
    {
        private const int MaxStackAllocSize = 4 * 1024;
        private const int MinUserStringLength = 4;
        private const int MaxUserStringLength = 128;

        private readonly UtfAllString[] strings;
        private readonly Trie<byte, int> utf8StringToIndex;
        private readonly bool readOnly;

        private int size;
        private UInt128 checksum;

        public JsonStringDictionary(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException($"{nameof(capacity)} must be a non negative integer.");
            }

            this.strings = new UtfAllString[capacity];
            this.utf8StringToIndex = new Trie<byte, int>(capacity);
            this.readOnly = false;
        }

        public JsonStringDictionary(IReadOnlyList<string> userStrings, bool readOnly)
        {
            this.readOnly = readOnly;

            if (userStrings == null)
            {
                throw new ArgumentNullException(nameof(userStrings));
            }

            for (int i = 0; i < userStrings.Count; i++)
            {
                string userString = userStrings[i];
                if (!this.TryAddString(Utf8Span.TranscodeUtf16(userString), out int index))
                {
                    throw new ArgumentException($"Failed to add {userString} to {nameof(JsonStringDictionary)}.");
                }

                if (index != i)
                {
                    throw new ArgumentException($"Tried to add {userString} at index {i}, but instead it was inserted at index {index}.");
                }
            }
        }

        public bool TryGetString(int index, out UtfAllString value)
        {
            if ((index < 0) || (index >= this.size))
            {
                value = default;
                return false;
            }

            value = this.strings[index];
            return true;
        }

        public bool TryGetIndex(Utf8Span value, out int index)
        {
            return utf8StringToIndex.TryGetValue(value.Span, out index);
        }

        public IJsonStringDictionary AsMutableJsonStringDictionary()
        {
            return this.readOnly ? null : this;
        }

        public bool TryAddString(string value, out int index)
        {
            int utf8Length = Encoding.UTF8.GetByteCount(value);
            Span<byte> utfString = utf8Length < JsonStringDictionary.MaxStackAllocSize ? stackalloc byte[utf8Length] : new byte[utf8Length];
            Encoding.UTF8.GetBytes(value, utfString);

            return this.TryAddString(Utf8Span.UnsafeFromUtf8BytesNoValidation(utfString), out index);
        }

        public bool TryAddString(Utf8Span value, out int index)
        {
            // If the string already exists, return that index.
            if (this.utf8StringToIndex.TryGetValue(value.Span, out index))
            {
                return true;
            }

            // Return false if dictionary already at capacity.
            if (this.size == this.strings.Length)
            {
                index = default;
                return false;
            }

            index = this.size;
            this.strings[this.size] = UtfAllString.Create(value.ToString());
            this.utf8StringToIndex.AddOrUpdate(value.Span, index);
            this.size++;

            return true;
        }

        public bool Equals(JsonStringDictionary other)
        {
            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null)
            {
                return false;
            }

            return this.checksum == other.checksum;
        }

        public bool Equals(IReadOnlyJsonStringDictionary other)
        {
            if (other == null)
            {
                return false;
            }

            if (!(other is JsonStringDictionary otherDictionary))
            {
                throw new NotImplementedException();
            }

            return this.Equals(otherDictionary);
        }

        public UInt128 GetChecksum()
        {
            return this.checksum;
        }

        private void SetChecksum()
        {
            UInt128 checksum = 0;
            for (int i = 0; i < this.size; i++)
            {
                checksum = MurmurHash3.Hash128(this.strings[i].Utf8String.Span.Span, checksum);
            }

            this.checksum = checksum;
        }
    }
#if INTERNAL
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
