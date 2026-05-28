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
    sealed class JsonStringDictionary : IJsonStringDictionary, IEquatable<JsonStringDictionary>
    {
        public static readonly int MaxDictionaryEncodedStrings = 
            TypeMarker.UserString1ByteLengthMax - TypeMarker.UserString1ByteLengthMin + ((TypeMarker.UserString2ByteLengthMax - TypeMarker.UserString2ByteLengthMin) * 256);

        private const int MaxStackAllocSize = 4 * 1024;
        private static readonly IReadOnlyList<string> EmptyUserStringList = new List<string>();

        private readonly List<UtfAllString> strings;
        private readonly Trie<byte, int> utf8StringToStringId;

        private int size;
        private UInt128 checksum;

        public JsonStringDictionary()
        : this(EmptyUserStringList)
        {
        }

        public JsonStringDictionary(IReadOnlyList<string> userStrings)
        {
            if (userStrings == null)
            {
                throw new ArgumentNullException(nameof(userStrings));
            }

            this.strings = new List<UtfAllString>();
            this.utf8StringToStringId = new Trie<byte, int>();

            for (int i = 0; i < userStrings.Count; i++)
            {
                string userString = userStrings[i];
                this.AddString(Utf8Span.TranscodeUtf16(userString), out int stringId);

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

        public bool Equals(IJsonStringDictionary other)
        {
            if (other == null)
            {
                return false;
            }

            if (other is JsonStringDictionary otherDictionary)
            {
                return this.Equals(otherDictionary);
            }
            else
            {
                if (other.GetCount() != this.size)
                {
                    return false;
                }

                for (int i = 0; i < this.size; i++)
                {
                    if (this.TryGetString(i, out UtfAllString userStringThis) &&
                        this.TryGetString(i, out UtfAllString userStringOther))
                    {
                        if (!userStringThis.Equals(userStringOther))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public int GetCount()
        {
            return this.size;
        }

        private void AddString(string value, out int stringId)
        {
            int utf8Length = Encoding.UTF8.GetByteCount(value);
            Span<byte> utfString = utf8Length < JsonStringDictionary.MaxStackAllocSize ? stackalloc byte[utf8Length] : new byte[utf8Length];
            Encoding.UTF8.GetBytes(value, utfString);

            this.AddString(Utf8Span.UnsafeFromUtf8BytesNoValidation(utfString), out stringId);
        }

        private void AddString(Utf8Span value, out int stringId)
        {
            stringId = this.size;
            this.strings.Add(UtfAllString.Create(value.ToString()));
            this.utf8StringToStringId.AddOrUpdate(value.Span, stringId);
            this.size++;
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
            UInt128 checksum = this.GetCount();
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
