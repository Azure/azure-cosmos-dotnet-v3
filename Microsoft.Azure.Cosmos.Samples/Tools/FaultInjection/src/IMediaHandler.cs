//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Collections;

    internal interface IMediaHandler
    {
        Task UploadMediaAsync(string mediaId,
            Stream mediaStream,
            INameValueCollection headers,
            int singleBlobUploadThresholdInBytes,
            TimeSpan blobUploadTimeoutSeconds);

        /// <returns>
        /// ResponseHeaders,
        /// Media Attributes
        /// </returns>
        Task<Tuple<INameValueCollection, INameValueCollection>> HeadMediaAsync(string mediaId,
            INameValueCollection headers = null);

        /// <returns>
        /// Stream,
        /// ResponseHeaders,
        /// Media Attributes
        /// </returns>
        Task<Tuple<Stream, INameValueCollection, INameValueCollection>> DownloadMediaAsync(string mediaId,
            INameValueCollection headers,
            TimeSpan blobDownloadTimeoutSeconds);

        Task DeleteMediaAsync(string mediaId, INameValueCollection headers = null);
    }
}
