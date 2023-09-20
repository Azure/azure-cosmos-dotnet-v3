// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using System.Text;

    internal static class StreamExtensions
    {
        public static string ReadAsString(this Stream stream)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                byte[] bytes = memoryStream.ToArray();
                return Encoding.UTF8.GetString(bytes);
            }
        }

        public static Stream ToStream(this string stringValue)
        {
            MemoryStream stream = new ();
            StreamWriter writer = new (stream);
            writer.Write(stringValue);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}
