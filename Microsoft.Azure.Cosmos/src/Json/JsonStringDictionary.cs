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

            if (size == this.stringDictionary.Length)
            {
                return false;
            }

            index = this.size;
            this.stringDictionary[size++] = value;
            this.stringToIndex[value] = index;

            return true;
        }

        public bool TryGetStringAtIndex(int index, out string value)
        {
            value = default(string);
            if (index < 0 || index >= size)
            {
                return false;
            }

            value = this.stringDictionary[index];
            return true;
        }
    }
#if INTERNAL
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
