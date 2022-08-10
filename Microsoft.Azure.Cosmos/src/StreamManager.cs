//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using Microsoft.IO;

    /// <summary>
    /// <see cref="StreamManager"/> encapsulates operations on top of pooling and managing memory streams which use potentially large buffers.
    /// </summary>
    internal static class StreamManager
    {
        // RecyclableMemoryStreamManager should be declared once and it will live for the entire process lifetime.
        // Reference: https://github.com/Microsoft/Microsoft.IO.RecyclableMemoryStream.
        private static readonly RecyclableMemoryStreamManager memoryStreamManager = new RecyclableMemoryStreamManager();

        public static Stream GetStream(string tag)
        {
            return memoryStreamManager.GetStream(tag);
        }

        public static Stream GetStream(string tag, int requiredSize)
        {
            return memoryStreamManager.GetStream(tag, requiredSize);
        }

        public static Stream GetStream(string tag, byte[] buffer)
        {
            return memoryStreamManager.GetStream(tag, buffer, 0, buffer.Length);
        }

        public static Stream GetStream(string tag, byte[] buffer, int offset, int count)
        {
            return memoryStreamManager.GetStream(tag, buffer, offset, count);
        }

        public static Stream GetReadonlyStream(byte[] buffer, int offset, int count)
        {
            return new MemoryStream(buffer, offset, count, writable: false, publiclyVisible: true);
        }
    }
}
