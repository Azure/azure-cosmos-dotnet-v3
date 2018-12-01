//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;

    /// <summary>
    /// The cosmos resource response class
    /// </summary>
    public abstract class CosmosResponse<T>
    {
        /// <summary>
        /// Create an empty cosmos response for mock testing
        /// </summary>
        public CosmosResponse()
        {

        }

        /// <summary>
        /// Create a CosmosResponse object with the default properties set
        /// </summary>
        /// <param name="cosmosResponseMessage"></param>
        internal CosmosResponse(CosmosResponseMessage cosmosResponseMessage)
        {
            this.Headers = cosmosResponseMessage.Headers;
            this.StatusCode = cosmosResponseMessage.StatusCode;
        }

        /// <summary>
        /// Gets the current <see cref="CosmosResponseMessage"/> HTTP headers.
        /// </summary>
        public virtual CosmosResponseMessageHeaders Headers { get; }

        /// <summary>
        /// The content of the response.
        /// </summary>
        public virtual T Resource { get; protected set; }

        /// <summary>
        /// Get Resource implicitly from <see cref="CosmosResponse{T}"/>
        /// </summary>
        public static implicit operator T(CosmosResponse<T> response)
        {
            return response.Resource;
        }

        /// <summary>
        /// Gets the request completion status code from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The request completion status code</value>
        public virtual HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in request units.
        /// </value>
        public virtual double RequestCharge => this.Headers.RequestCharge;

        /// <summary>
        /// Gets the activity ID for the request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        public virtual string ActivityId => this.Headers.ActivityId;

        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources. 
        /// </remarks>
        public virtual string ETag => this.Headers.ETag;

        /// <summary>
        /// Gets the maximum size limit for this entity from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum size limit for this entity. Measured in kilobytes for document resources 
        /// and in counts for other resources.
        /// </value>
        /// <remarks>
        /// To get public access to the quota information do the following
        /// cosmosResponse.Headers.GetHeaderValue("x-ms-resource-quota")
        /// </remarks>
        internal virtual string MaxResourceQuota => this.Headers.GetHeaderValue<string>(HttpConstants.HttpHeaders.MaxResourceQuota);

        /// <summary>
        /// Gets the current size of this entity from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The current size for this entity. Measured in kilobytes for document resources 
        /// and in counts for other resources.
        /// </value>
        /// <remarks>
        /// To get public access to the quota information do the following
        /// cosmosResponse.Headers.GetHeaderValue("x-ms-resource-usage")
        /// </remarks>
        internal virtual string CurrentResourceQuotaUsage => this.Headers.GetHeaderValue<string>(HttpConstants.HttpHeaders.CurrentResourceQuotaUsage);

        /// <summary>
        /// Gets the stream body and converts it to the typed passed in.
        /// This can throw JSON serialization exceptions
        /// </summary>
        /// <param name="cosmosResponseMessage">The response from the Cosmos service</param>
        /// <param name="cosmosJsonSerializer">The JSON serializer</param>
        private void InitializeResource(CosmosResponseMessage cosmosResponseMessage, CosmosJsonSerializer cosmosJsonSerializer)
        {
            this.Resource = ToObjectInternal(cosmosResponseMessage, cosmosJsonSerializer);
        }
        
        internal T ToObjectInternal(CosmosResponseMessage cosmosResponseMessage, CosmosJsonSerializer cosmosJsonSerializer)
        {
            // Not finding something is part of a normal work-flow and should not be an exception.
            // This prevents the unnecessary overhead of an exception
            if (cosmosResponseMessage.StatusCode == HttpStatusCode.NotFound)
            {
                return default(T);
            }

            //Throw the exception
            cosmosResponseMessage.EnsureSuccessStatusCode();

            if (cosmosResponseMessage.Content == null)
            {
                return default(T);
            }

            return cosmosJsonSerializer.FromStream<T>(cosmosResponseMessage.Content);
        }

        /// <summary>
        /// Create the cosmos response using the custom JSON parser.
        /// This factory exist to ensure that the HttpResponseMessage is
        /// correctly disposed of, and to handle the async logic required to
        /// read the stream and to convert it to an object.
        /// </summary>
        internal static CosmosResponseType InitResponse<CosmosResponseType, CosmosSettingsType>(
            Func<CosmosResponseMessage, CosmosResponseType> createCosmosResponse,
            CosmosJsonSerializer jsonSerializer,
            CosmosResponseMessage cosmosResponseMessage,
            CosmosIdentifier cosmosSetResource = null)
            where CosmosResponseType : CosmosResponse<CosmosSettingsType>
        {
            using (cosmosResponseMessage)
            {
                CosmosResponseType cosmosResponse = createCosmosResponse(cosmosResponseMessage);
                cosmosResponse.InitializeResource(cosmosResponseMessage, jsonSerializer);
                return cosmosResponse;
            }
        }
    }
}