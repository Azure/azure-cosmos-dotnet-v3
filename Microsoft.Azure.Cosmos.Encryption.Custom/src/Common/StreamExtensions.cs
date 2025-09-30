//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Extension methods for Stream to provide compatibility across different .NET versions.
    /// </summary>
    internal static class StreamExtensions
    {
        /// <summary>
        /// Asynchronously disposes the stream in a version-compatible way.
        /// Uses DisposeAsync on .NET 8.0+ and falls back to synchronous Dispose on earlier versions.
        /// </summary>
        /// <param name="stream">The stream to dispose.</param>
        /// <returns>A ValueTask representing the asynchronous dispose operation.</returns>
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
