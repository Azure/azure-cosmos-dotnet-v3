// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// IOpenTelemetryResponse
    /// </summary>
    internal class OpenTelemetryResponse : IOpenTelemetryResponse
    {
        public OpenTelemetryResponse(ResponseMessage message)
        {
            this.StatusCode = message.StatusCode;
        }

        public OpenTelemetryResponse(UserResponse message)
        {
            this.StatusCode = message.StatusCode;
        }
        
        public OpenTelemetryResponse(PermissionResponse message)
        {
            this.StatusCode = message.StatusCode;
        }

        public OpenTelemetryResponse(ContainerResponse message)
        {
            this.StatusCode = message.StatusCode;
        }
        
        public OpenTelemetryResponse(DatabaseResponse message)
        {
            this.StatusCode = message.StatusCode;
        }

        public OpenTelemetryResponse(ThroughputResponse message)
        {
            this.StatusCode = message.StatusCode;
        }
        
        public OpenTelemetryResponse(TransactionalBatchResponse message)
        {
            this.StatusCode = message.StatusCode;
        }
        
        public OpenTelemetryResponse(FeedResponse<ChangeFeedProcessorState> message)
        {
            this.StatusCode = message.StatusCode;
        }
        
        public OpenTelemetryResponse(ClientEncryptionKeyResponse message)
        {
            this.StatusCode = message.StatusCode;
        }

        public OpenTelemetryResponse(FeedResponse<dynamic> message)
        {
            this.StatusCode = message.StatusCode;
        }

        public OpenTelemetryResponse(ItemResponse<dynamic> message)
        {
            this.StatusCode = message.StatusCode;
        }

        /// <summary>
        /// StatusCode
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// RequestCharge
        /// </summary>
        public double RequestCharge { get; }

        /// <summary>
        /// RequestLength
        /// </summary>
        public long? RequestContentLength { get; }

        /// <summary>
        /// ResponseLength
        /// </summary>
        public long? ResponseContentLength { get; }

        /// <summary>
        /// ContainerName
        /// </summary>
        public string ContainerName { get; }

        /// <summary>
        /// ItemCount
        /// </summary>
        public string ItemCount { get; }

    }
}
