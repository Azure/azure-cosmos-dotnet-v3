// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class JsonStringDictionary
    {
        private readonly string[] stringDictionary;
        private readonly Dictionary<string, int> stringToIndex;
        private int size;

        public JsonStringDictionary(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException($"{nameof(capacity)} must be a non negative integer.");
            }

            this.stringDictionary = new string[capacity];
            this.stringToIndex = new Dictionary<string, int>();
        }

        public bool TryAddString(string value, out int index)
        {
            index = default(int);
            if (this.stringToIndex.TryGetValue(value, out index))
            {
                // If the string already exists just leave.
                return true;
            }

            if (this.size == this.stringDictionary.Length)
            {
                return false;
            }

            index = this.size;
            this.stringDictionary[this.size++] = value;
            this.stringToIndex[value] = index;

            return true;
        }

        public bool TryGetStringAtIndex(int index, out string value)
        {
            value = default(string);
            if (index < 0 || index >= this.size)
            {
                return false;
            }

            value = this.stringDictionary[index];
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
                if (!jsonStringDictionary.TryAddString(userString, out int index))
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
