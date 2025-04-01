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
    sealed class JsonStringDictionary : IJsonReadOnlyStringDictionary, IEquatable<JsonStringDictionary>
    {
        private const int MaxStackAllocSize = 4 * 1024;
        private const int MaxDictionarySize = 
            TypeMarker.UserString1ByteLengthMax - TypeMarker.UserString1ByteLengthMin + ((TypeMarker.UserString2ByteLengthMax - TypeMarker.UserString2ByteLengthMin) * 0xFF);

        private readonly List<UtfAllString> strings;
        private readonly Trie<byte, int> utf8StringToStringId;

        private int size;
        private UInt128 checksum;

        public JsonStringDictionary()
        : this(new List<string>())
        {
        }

        public JsonStringDictionary(IReadOnlyList<string> userStrings)
        {
            this.strings = new List<UtfAllString>();
            this.utf8StringToStringId = new Trie<byte, int>();

            if (userStrings == null)
            {
                throw new ArgumentNullException(nameof(userStrings));
            }

            for (int i = 0; i < userStrings.Count; i++)
            {
                string userString = userStrings[i];
                if (!this.TryAddString(Utf8Span.TranscodeUtf16(userString), MaxDictionarySize, out int stringId))
                {
                    throw new ArgumentException($"Failed to add {userString} to {nameof(JsonStringDictionary)}.");
                }

                if (stringId != i)
                {
                    throw new ArgumentException($"Tried to add {userString} at stringId {i}, but instead it was inserted at stringId {stringId}.");
                }
            }

            this.SetChecksum();
        }

        public bool TryGetString(int stringId, out UtfAllString value)
        {
            if ((stringId < 0) || (stringId >= this.size))
            {
                value = default;
                return false;
            }

            value = this.strings[stringId];
            return true;
        }

        public bool TryGetStringId(Utf8Span value, out int stringId)
        {
            return this.utf8StringToStringId.TryGetValue(value.Span, out stringId);
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

        public bool Equals(IJsonReadOnlyStringDictionary other)
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

        private bool TryAddString(string value, int maxCount, out int stringId)
        {
            int utf8Length = Encoding.UTF8.GetByteCount(value);
            Span<byte> utfString = utf8Length < JsonStringDictionary.MaxStackAllocSize ? stackalloc byte[utf8Length] : new byte[utf8Length];
            Encoding.UTF8.GetBytes(value, utfString);

            return this.TryAddString(Utf8Span.UnsafeFromUtf8BytesNoValidation(utfString), maxCount, out stringId);
        }

        private bool TryAddString(Utf8Span value, int maxCount, out int stringId)
        {
            // If the string already exists, return that stringId.
            if (this.utf8StringToStringId.TryGetValue(value.Span, out stringId))
            {
                return true;
            }

            // Return false if dictionary already at capacity.
            if (this.size == maxCount)
            {
                stringId = default;
                return false;
            }

            stringId = this.size;
            this.strings.Add(UtfAllString.Create(value.ToString()));
            this.utf8StringToStringId.AddOrUpdate(value.Span, stringId);
            this.size++;

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
