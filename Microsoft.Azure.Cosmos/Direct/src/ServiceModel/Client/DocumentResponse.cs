//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Client
{
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents the template class used by methods returning single objects in the Azure Cosmos DB service.
    /// </summary> 
    /// <typeparam name="TDocument">the document type.</typeparam>
    /// <remarks>
    /// Response from type-specific read of Document resource(ReadDocumentAsync{TDocument}) returns the response wrapped in a 
    /// DocumentResponse object. This contains the metadata from the response headers from the Azure Cosmos DB call including
    /// the request units (RequestCharge), activity ID, quotas/usage of resources and the typed document object(TDocument).
    /// </remarks>
    /// <example>
    /// The following example extracts the CustomerName property, request units consumed, activity ID and StatusCode from a ReadDocumentAsync{Customer} call.
    /// <code language="c#">
    /// <![CDATA[
    /// DocumentResponse<Customer> response = await client.ReadDocumentAsync<Customer>(documentLink);
    /// Console.WriteLine(response.Document.CustomerName);
    /// Console.WriteLine(response.RequestCharge);
    /// Console.WriteLine(response.ActivityId); 
    /// Console.WriteLine(response.StatusCode); // HttpStatusCode.Created or 201
    /// ]]>
    /// </code>
    /// </example>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class DocumentResponse<TDocument> : ResourceResponseBase, IDocumentResponse<TDocument>
    {
        private TDocument document;
        private JsonSerializerSettings settings;

        /// <summary>
        /// Constructor exposed for mocking purposes for the Azure Cosmos DB service.
        /// </summary>
        public DocumentResponse()
        {

        }

        /// <summary>
        /// Constructor exposed for mocking purposes for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="document"></param>
        public DocumentResponse(TDocument document)
            :this()
        {
            this.document = document;
        }

        internal DocumentResponse(DocumentServiceResponse response, JsonSerializerSettings settings = null)
            :base(response)
        {
            this.settings = settings;
        }

        /// <summary>
        /// Gets the document returned in the response from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The document returned in the response.
        /// </value>
        public TDocument Document
        {
            get
            {
                if (this.document == null)
                {
                    Document doc = this.response.GetResource<Document>();
                    doc.SerializerSettings = this.settings;
                    this.document = (TDocument)(dynamic)doc;
                }
                return this.document;
            }
        }

        /// <summary>
        /// Returns the document in the response implicitly from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="source">The DocumentResponse source.</param>
        /// <returns>The document object.</returns>
        public static implicit operator TDocument(DocumentResponse<TDocument> source)
        {
            return source.Document;
        }
    }
}
