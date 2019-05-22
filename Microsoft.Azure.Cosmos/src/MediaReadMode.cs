//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary> 
    /// Represents the mode for use with downloading attachment content (a.k.a. media) in the Azure Cosmos DB service.
    /// </summary>
    internal enum MediaReadMode
    {
        /// <summary>
        /// Content is buffered at the client and not directly streamed from the content store. Use Buffered to reduce the time taken to read and write media files.
        /// </summary>
        Buffered,

        /// <summary>
        /// Content is directly streamed from the content store without any buffering at the client. Use Streamed to reduce the client memory overhead of reading and writing media files.
        /// </summary>
        Streamed
    }
}