//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.IO;
    using System.Threading.Tasks;

    internal static class StreamExtensions
    {
        public static ValueTask DisposeCompatAsync(this Stream stream)
        {
#if NET8_0_OR_GREATER
            return stream.DisposeAsync();
#else
            stream.Dispose();
            return default;
#endif
        }
    }
}
