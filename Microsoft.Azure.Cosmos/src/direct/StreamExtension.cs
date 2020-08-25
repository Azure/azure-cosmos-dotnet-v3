//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;
     
    internal static class StreamExtension
    {
        async public static Task CopyToAsync(this Stream srcStream,
            Stream destinationStream,
            long maxSizeToCopy = long.MaxValue)
        {
            if (srcStream == null)
            {
                throw new ArgumentNullException("srcStream");
            }

            if (destinationStream == null)
            {
                throw new ArgumentNullException("destinationStream");
            }

            byte[] buffer = new byte[RuntimeConstants.Serialization.ChunkSize1K]; //Copy them in chunks of 1 KB.

            long numberOfBytesRead = 0;

            while(true)
            {
                int bytesRead = await srcStream.ReadAsync(buffer, 0, RuntimeConstants.Serialization.ChunkSize1K);

                if (bytesRead <= 0)
                {
                    return;
                }

                numberOfBytesRead += bytesRead;
                
                if(numberOfBytesRead > maxSizeToCopy)
                {
                    throw new RequestEntityTooLargeException(
                        RMResources.RequestTooLarge);
                }
                await destinationStream.WriteAsync(buffer, 0, bytesRead);
            }            
        }

        /// <summary>
        /// Creates a nonwritable MemoryStream with exposable buffer which enables TryGetBuffer to reduce allocations.
        /// </summary>
        public static MemoryStream CreateExportableMemoryStream(byte[] body)
        {
            return new MemoryStream(buffer: body,
                            index: 0,
                            count: body.Length,
                            writable: false,
                            publiclyVisible: true);
        }

#if NETFX45 || NETSTANDARD15 || NETSTANDARD16
        public static Task<CloneableStream> AsClonableStreamAsync(Stream mediaStream)
        {
            return StreamExtension.CopyStreamAndReturnAsync(mediaStream);
        }
#else
        public static Task<CloneableStream> AsClonableStreamAsync(Stream mediaStream)
        {
            MemoryStream memoryStream = mediaStream as MemoryStream;
            if (memoryStream != null && memoryStream.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                return Task.FromResult(new CloneableStream(memoryStream));
            }

            return StreamExtension.CopyStreamAndReturnAsync(mediaStream);
        }
#endif

        private static async Task<CloneableStream> CopyStreamAndReturnAsync(Stream mediaStream)
        {
            MemoryStream memoryStreamClone = new MemoryStream();
            if (mediaStream.CanSeek)
            {
                mediaStream.Position = 0;
            }

            await mediaStream.CopyToAsync(memoryStreamClone);
            memoryStreamClone.Position = 0;
            return new CloneableStream(memoryStreamClone);
        }
    }
}
