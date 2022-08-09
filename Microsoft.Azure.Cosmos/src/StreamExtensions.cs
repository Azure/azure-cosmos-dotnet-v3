// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using System.Text;
    using Microsoft.IO;

    internal static class StreamExtensions
    {
        public static string ReadAsString(this Stream stream)
        {
            using (RecyclableMemoryStream memoryStream = StreamManager.GetStream(nameof(ReadAsString)) as RecyclableMemoryStream)
            {
                stream.CopyTo(memoryStream);
                byte[] bytes = memoryStream.GetBuffer();
                return Encoding.UTF8.GetString(bytes);
            }
        }
    }
}
