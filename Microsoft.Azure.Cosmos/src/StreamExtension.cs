//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

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

            while (true)
            {
                int bytesRead = await srcStream.ReadAsync(buffer, 0, RuntimeConstants.Serialization.ChunkSize1K);

                if (bytesRead <= 0)
                {
                    return;
                }

                numberOfBytesRead += bytesRead;

                if (numberOfBytesRead > maxSizeToCopy)
                {
                    throw new RequestEntityTooLargeException(
                        RMResources.RequestTooLarge);
                }
                await destinationStream.WriteAsync(buffer, 0, bytesRead);
            }
        }
    }
}
