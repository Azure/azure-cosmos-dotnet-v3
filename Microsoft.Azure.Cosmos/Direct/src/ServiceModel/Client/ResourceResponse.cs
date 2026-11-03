//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Client
{
    /// <summary>
    /// Represents the template class used by methods returning single objects in the Azure Cosmos DB service.
    /// </summary> 
    /// <typeparam name="TResource">the resource type.</typeparam>
    /// <remarks>
    /// All responses from creates, reads, updates and deletes of Azure Cosmos DB resources return the response wrapped in a 
    /// ResourceResponse object. This contains the metadata from the response headers from the Azure Cosmos DB call including
    /// the request units (RequestCharge), activity ID and quotas/usage of resources.
    /// </remarks>
    /// <example>
    /// The following example extracts the request units consumed, activity ID and StatusCode from a CreateDocumentAsync call.
    /// <code language="c#">
    /// <![CDATA[
    /// ResourceResponse<Document> response = await client.CreateDocumentAsync(collectionLink, document);
    /// Console.WriteLine(response.RequestCharge);
    /// Console.WriteLine(response.ActivityId); 
    /// Console.WriteLine(response.StatusCode); // HttpStatusCode.Created or 201
    /// ]]>
    /// </code>
    /// </example>
    /// <seealso cref="Resource"/>
    /// <seealso cref="FeedResponse{T}"/>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    class ResourceResponse<TResource> : ResourceResponseBase, IResourceResponse<TResource> where TResource : Resource, new()
    {
        private TResource resource;
        private ITypeResolver<TResource> typeResolver;

        /// <summary>
        /// Constructor exposed for mocking purposes for the Azure Cosmos DB service.
        /// </summary>
        public ResourceResponse()
        {

        }

        /// <summary>
        /// Constructor exposed for mocking purposes for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="resource"></param>
        public ResourceResponse(TResource resource)
            :this()
        {
            this.resource = resource;
        }

        internal ResourceResponse(DocumentServiceResponse response, ITypeResolver<TResource> typeResolver = null)
            :base(response)
        {
            this.typeResolver = typeResolver;
        }

        /// <summary>
        /// Gets the resource returned in the response from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The resource returned in the response.
        /// </value>
        public TResource Resource
        {
            get
            {
                if (this.resource == null)
                {
                    this.resource = this.response.GetResource<TResource>(typeResolver);
                }
                return this.resource;
            }
        }

        /// <summary>
        /// Returns the resource in the response implicitly from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="source">The ResourceResponse source.</param>
        /// <returns>The resource object.</returns>
        public static implicit operator TResource(ResourceResponse<TResource> source)
        {
            return source.Resource;
        }
    }
}
