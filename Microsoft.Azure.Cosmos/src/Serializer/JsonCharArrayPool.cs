//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Buffers;
    using Newtonsoft.Json;

    /// <summary>
    /// Adapts <see cref="ArrayPool{T}.Shared"/> to the JSON.NET <see cref="IArrayPool{T}"/> contract so
    /// <see cref="JsonTextReader"/> and <see cref="JsonTextWriter"/> can rent their internal buffers
    /// instead of allocating new ones for every (de)serialization. Arrays are cleared when returned.
    /// </summary>
    internal sealed class JsonCharArrayPool : IArrayPool<char>
    {
        public static readonly JsonCharArrayPool Instance = new JsonCharArrayPool();

        private JsonCharArrayPool()
        {
        }

        public char[] Rent(int minimumLength)
        {
            return ArrayPool<char>.Shared.Rent(minimumLength);
        }

        public void Return(char[] array)
        {
            // The buffers hold document content, which may be sensitive (for example decrypted
            // values), so clear them before they can be rented by any other code in the process.
            ArrayPool<char>.Shared.Return(array, clearArray: true);
        }
    }
}
