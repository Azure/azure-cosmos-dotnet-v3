// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;

    internal sealed class JsonStringDictionary
    {
        private readonly string[] stringDictionary;
        private int size;

        private JsonStringDictionary(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException($"{nameof(capacity)} must be a non negative integer.");
            }

            this.stringDictionary = new string[capacity];
        }

        public bool TryAddString(string value, out int index)
        {
            index = default(int);

            if (size == this.stringDictionary.Length)
            {
                return false;
            }

            index = this.size;
            this.stringDictionary[size++] = value;

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
}
