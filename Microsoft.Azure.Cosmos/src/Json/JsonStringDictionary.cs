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
    using static Microsoft.Azure.Cosmos.Json.JsonBinaryEncoding;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class JsonStringDictionary : IReadOnlyJsonStringDictionary, IEquatable<JsonStringDictionary>
    {
        private const int MaxStackAllocSize = 4 * 1024;
        private const int MaxDictionarySize = TypeMarker.UserString1ByteLengthMax - TypeMarker.UserString1ByteLengthMin + ((TypeMarker.UserString2ByteLengthMax - TypeMarker.UserString2ByteLengthMin) * 0xFF);

        private readonly List<UtfAllString> strings;
        private readonly Trie<byte, int> utf8StringToIndex;

        private int size;
        private UInt128 checksum;

        public JsonStringDictionary()
        {
            this.strings = new List<UtfAllString>();
            this.utf8StringToIndex = new Trie<byte, int>();
            this.SetChecksum();
        }

        public JsonStringDictionary(IReadOnlyList<string> userStrings)
        {
            this.strings = new List<UtfAllString>();
            this.utf8StringToIndex = new Trie<byte, int>();

            if (userStrings == null)
            {
                throw new ArgumentNullException(nameof(userStrings));
            }

            for (int i = 0; i < userStrings.Count; i++)
            {
                string userString = userStrings[i];
                if (!this.TryAddString(Utf8Span.TranscodeUtf16(userString), MaxDictionarySize, out int index))
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
            return this.utf8StringToIndex.TryGetValue(value.Span, out index);
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

        public int GetCount()
        {
            return this.strings.Count;
        }

        private bool TryAddString(string value, int maxCount, out int index)
        {
            int utf8Length = Encoding.UTF8.GetByteCount(value);
            Span<byte> utfString = utf8Length < JsonStringDictionary.MaxStackAllocSize ? stackalloc byte[utf8Length] : new byte[utf8Length];
            Encoding.UTF8.GetBytes(value, utfString);

            return this.TryAddString(Utf8Span.UnsafeFromUtf8BytesNoValidation(utfString), maxCount, out index);
        }

        private bool TryAddString(Utf8Span value, int maxCount, out int index)
        {
            // If the string already exists, return that index.
            if (this.utf8StringToIndex.TryGetValue(value.Span, out index))
            {
                return true;
            }

            // Return false if dictionary already at capacity.
            if (this.size == maxCount)
            {
                index = default;
                return false;
            }

            index = this.size;
            this.strings.Add(UtfAllString.Create(value.ToString()));
            this.utf8StringToIndex.AddOrUpdate(value.Span, index);
            this.size++;

            this.SetChecksum();

            return true;
        }

        public override int GetHashCode()
        {
            return this.checksum.GetHashCode();
        }

        internal UInt128 GetChecksum()
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
