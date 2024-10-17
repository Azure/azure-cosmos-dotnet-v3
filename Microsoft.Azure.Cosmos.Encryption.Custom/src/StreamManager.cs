// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.IO;
    using System.Threading.Tasks;

    // Recyclable memory stream should be here
    internal class StreamManager
    {
        public Stream CreateStream(int hintSize = 0)
        {
            return new MemoryStream(hintSize);
        }

        public async ValueTask ReturnStreamAsync(Stream stream)
        {
            await stream.DisposeAsync();
        }
    }
}
#endif