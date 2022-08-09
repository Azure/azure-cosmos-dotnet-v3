namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.IO;

    internal static class StreamManager
    {
        private static readonly RecyclableMemoryStreamManager memoryStreamManager = new RecyclableMemoryStreamManager();

        public static Stream GetStream(string tag)
        {
            return memoryStreamManager.GetStream(tag: tag);
        }
    }
}
