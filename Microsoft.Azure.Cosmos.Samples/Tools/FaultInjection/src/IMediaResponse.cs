//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Specialized;
    using System.IO;

    /// <summary>
    /// Captures the APIs for responses associated with media resource in the Azure Cosmos DB service.
    /// Interface exposed for mocking purposes.
    /// </summary>
    internal interface IMediaResponse
    {
        /// <summary> 
        /// Gets the Activity ID for the request associated with the media resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The Activity ID for the request.</value>
        string ActivityId { get; }

        /// <summary>
        /// Gets the HTTP ContentLength header value for the response associated with the media resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The HTTP ContentLength header value.</value>
        long ContentLength { get; }

        /// <summary>
        /// Gets the HTTP ContentType header value for the response associated with the media resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The HTTP ContentType header value.</value>
        string ContentType { get; }

        /// <summary>
        /// Gets the current attachment content (media) usage in megabytes for the media resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The current attachment content (media) usage in megabytes.</value>
        /// <remarks>This value is retrieved from the gateway. The value is returned from
        /// cached information updated periodically and is not guaranteed to be real time.
        /// </remarks>
        long CurrentMediaStorageUsageInMB { get; }

        /// <summary>
        /// Gets the attachment content (media) storage quota in megabytes for the media resource in the Azure Cosmos DB service. Retrieved from gateway.
        /// </summary>
        /// <value>The attachment content (media) storage quota in megabytes.</value>
        long MaxMediaStorageUsageInMB { get; }

        /// <summary>
        /// Gets the attachment content stream for the media resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The attachment content stream.</value>
        Stream Media { get; }

        /// <summary>
        /// Gets the headers associated with the response associated with the media resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The headers associated with the response.</value>
        NameValueCollection ResponseHeaders { get; }

        /// <summary>
        /// Gets the HTTP slug header value for the response associcated with the media resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The HTTP slug header value.</value>
        string Slug { get; }
    }
}
