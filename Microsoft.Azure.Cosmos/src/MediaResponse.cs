//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Specialized;
    using System.IO;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// Represents the response associated with retrieving attachment content from the Azure Cosmos DB service.
    /// </summary>
    internal sealed class MediaResponse : IMediaResponse
    {
        /// <summary>
        /// Constructor exposed for mocking purposes for the Azure Cosmos DB service.
        /// </summary>
        public MediaResponse()
        {
        }

        /// <summary> 
        /// Gets or sets the Activity ID for the request in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The Activity ID for the request.</value>
        public string ActivityId
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets or sets the attachment content stream in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The attachment content stream.</value>
        public Stream Media
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets or sets the HTTP slug header value in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The HTTP slug header value.</value>
        public string Slug
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets or sets the HTTP ContentType header value in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The HTTP ContentType header value.</value>
        public string ContentType
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets or sets the HTTP ContentLength header value in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The HTTP ContentLength header value.</value>
        public long ContentLength
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets the attachment content (media) storage quota in megabytes from the Azure Cosmos DB service.
        /// Retrieved from gateway.
        /// </summary>
        /// <value>The attachment content (media) storage quota in megabytes.</value>
        public long MaxMediaStorageUsageInMB
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets the current attachment content (media) usage in megabytes from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The current attachment content (media) usage in megabytes.</value>
        /// <remarks>This value is retrieved from the gateway.
        /// The value is returned from
        /// cached information updated periodically and is not guaranteed to be real time.
        /// </remarks>
        public long CurrentMediaStorageUsageInMB
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets the headers associated with the response from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The headers associated with the response.</value>
        public NameValueCollection ResponseHeaders
        {
            get { return this.Headers.ToNameValueCollection(); }
        }

        internal INameValueCollection Headers { get; set; }
    }
}
