// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Collections;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class JsonStringDictionary : IReadOnlyJsonStringDictionary
    {
        private const int MaxStackAllocSize = 4 * 1024;

        private readonly ReadOnlyMemory<byte>[] utf8Strings;
        private readonly string[] utf16Strings;
        private readonly Trie<byte, int> utf8StringToIndex;

        private int size;

        public JsonStringDictionary(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException($"{nameof(capacity)} must be a non negative integer.");
            }

            this.utf8Strings = new ReadOnlyMemory<byte>[capacity];
            this.utf16Strings = new string[capacity];
            this.utf8StringToIndex = new Trie<byte, int>(capacity);
        }

        public bool TryAddString(string value, out int index)
        {
            int utf8Length = Encoding.UTF8.GetByteCount(value);
            Span<byte> utfString = utf8Length < JsonStringDictionary.MaxStackAllocSize ? stackalloc byte[utf8Length] : new byte[utf8Length];
            Encoding.UTF8.GetBytes(value, utfString);

            return this.TryAddString(utfString, out index);
        }

        public bool TryAddString(ReadOnlySpan<byte> value, out int index)
        {
            // If the string already exists, then just return that index.
            if (this.utf8StringToIndex.TryGetValue(value, out index))
            {
                return true;
            }

            // If we are at capacity just return false.
            if (this.size == this.utf8Strings.Length)
            {
                index = default;
                return false;
            }

            index = this.size;
            this.utf8Strings[this.size] = value.ToArray();
            this.utf16Strings[this.size] = Encoding.UTF8.GetString(value);
            this.utf8StringToIndex.AddOrUpdate(value, index);
            this.size++;

            return true;
        }

        public bool TryGetStringAtIndex(int index, out string value)
        {
            if ((index < 0) || (index >= this.size))
            {
                value = default;
                return false;
            }

            value = this.utf16Strings[index];
            return true;
        }

        public bool TryGetUtf8StringAtIndex(int index, out ReadOnlyMemory<byte> value)
        {
            if ((index < 0) || (index >= this.size))
            {
                value = default;
                return false;
            }

            value = this.utf8Strings[index];
            return true;
        }

        public static JsonStringDictionary CreateFromStringArray(IReadOnlyList<string> userStrings)
        {
            if (userStrings == null)
            {
                throw new ArgumentNullException(nameof(userStrings));
            }

            JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(userStrings.Count);
            for (int i = 0; i < userStrings.Count; i++)
            {
                string userString = userStrings[i];
                if (!jsonStringDictionary.TryAddString(Encoding.UTF8.GetBytes(userString).AsSpan(), out int index))
                {
                    throw new ArgumentException($"Failed to add {userString} to {nameof(JsonStringDictionary)}.");
                }

                if (index != i)
                {
                    throw new ArgumentException($"Tried to add {userString} at index {i}, but instead it was inserted at index {index}.");
                }
            }

            return jsonStringDictionary;
        }
    }
#if INTERNAL
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
