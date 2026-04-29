//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Newtonsoft.Json;

    /// <summary>
    /// Centralized JSON format used to serialize and deserialize in-memory lease state
    /// to/from a <see cref="MemoryStream"/>. Co-locating the format here prevents silent
    /// drift between the writer (<see cref="DocumentServiceLeaseContainerInMemory.ShutdownAsync"/>)
    /// and the reader (<see cref="DocumentServiceLeaseStoreManagerInMemory"/>).
    /// </summary>
    /// <remarks>
    /// Each <see cref="DocumentServiceLease"/> carries a Timestamp field that is serialized verbatim.
    /// If the elapsed time between stopping a processor and starting the next one exceeds the configured
    /// lease expiration interval (default 60 seconds), restored leases will initially appear expired and
    /// the current host will re-acquire each one on the first balancing cycle after StartAsync. This is
    /// a one-time self-healing event per restored lease; it does not cause data loss and, because the
    /// in-memory container is single-host by design, it does not cause ownership flapping. The only
    /// observable effect is a burst of lease-acquire trace messages at startup.
    /// </remarks>
    internal static class InMemoryLeaseJsonFormat
    {
        /// <summary>
        /// StreamReader/Writer default buffer size. Exposed as a constant so the read and
        /// write paths cannot drift apart.
        /// </summary>
        private const int BufferSize = 1024;

        /// <summary>
        /// UTF-8 without BOM. Matches the default StreamWriter encoding for portability.
        /// </summary>
        private static readonly Encoding SerializationEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary>
        /// Serializes <paramref name="leases"/> to a byte array using the in-memory lease JSON format.
        /// </summary>
        public static byte[] Serialize(IReadOnlyCollection<DocumentServiceLease> leases)
        {
            using (MemoryStream temp = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(temp, encoding: SerializationEncoding, bufferSize: BufferSize, leaveOpen: true))
                using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
                {
                    JsonSerializer serializer = JsonSerializer.Create();
                    serializer.Serialize(jsonWriter, leases);
                }

                return temp.ToArray();
            }
        }

        /// <summary>
        /// Deserializes an array of <see cref="DocumentServiceLease"/> previously produced by
        /// <see cref="Serialize"/>. Returns an empty list when <paramref name="source"/> is empty
        /// or null. Throws <see cref="JsonException"/> when the stream content is not valid JSON
        /// in the expected shape; callers are responsible for wrapping that into an implementation
        /// specific exception.
        /// </summary>
        public static List<DocumentServiceLease> Deserialize(Stream source)
        {
            if (source == null || source.Length == 0)
            {
                return new List<DocumentServiceLease>();
            }

            source.Position = 0;

            using (StreamReader sr = new StreamReader(
                source,
                encoding: SerializationEncoding,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: BufferSize,
                leaveOpen: true))
            using (JsonTextReader jsonReader = new JsonTextReader(sr))
            {
                JsonSerializer serializer = JsonSerializer.Create();
                return serializer.Deserialize<List<DocumentServiceLease>>(jsonReader) ?? new List<DocumentServiceLease>();
            }
        }
    }
}
