//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Cosmos item request options
    /// </summary>
    public class ItemRequestOptions : RequestOptions
    {
        /// <summary>
        /// Gets or sets the trigger to be invoked before the operation in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The trigger to be invoked before the operation.
        /// </value>
        /// <remarks>
        /// Only valid when used with Create, Replace and Delete methods for documents.
        /// Currently only one PreTrigger is permitted per operation.
        /// </remarks>
        public IEnumerable<string> PreTriggers { get; set; }

        /// <summary>
        /// Gets or sets the trigger to be invoked after the operation in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The trigger to be invoked after the operation.
        /// </value>
        /// <remarks>
        /// Only valid when used with Create, Replace and Delete methods for documents.
        /// Currently only one PreTrigger is permitted per operation.
        /// </remarks>
        public IEnumerable<string> PostTriggers { get; set; }

        /// <summary>
        /// Gets or sets the indexing directive (Include or Exclude) for the request in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The indexing directive to use with a request.
        /// </value>
        /// <seealso cref="Microsoft.Azure.Cosmos.IndexingPolicy"/>
        /// <seealso cref="IndexingDirective"/>
        public IndexingDirective? IndexingDirective { get; set; }

        /// <summary>
        /// Gets or sets the token for use with session consistency in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The token for use with session consistency.
        /// </value>
        ///
        /// <remarks>
        /// One of the <see cref="ConsistencyLevel"/> for Azure Cosmos DB is Session. In fact, this is the default level applied to accounts.
        /// <para>
        /// When working with Session consistency, each new write request to Azure Cosmos DB is assigned a new SessionToken.
        /// The DocumentClient will use this token internally with each read/query request to ensure that the set consistency level is maintained.
        ///
        /// <para>
        /// In some scenarios you need to manage this Session yourself;
        /// Consider a web application with multiple nodes, each node will have its own instance of <see cref="DocumentClient"/>
        /// If you wanted these nodes to participate in the same session (to be able read your own writes consistently across web tiers)
        /// you would have to send the SessionToken from <see cref="ItemResponse{T}"/> of the write action on one node
        /// to the client tier, using a cookie or some other mechanism, and have that token flow back to the web tier for subsequent reads.
        /// If you are using a round-robin load balancer which does not maintain session affinity between requests, such as the Azure Load Balancer,
        /// the read could potentially land on a different node to the write request, where the session was created.
        /// </para>
        ///
        /// <para>
        /// If you do not flow the Azure Cosmos DB SessionToken across as described above you could end up with inconsistent read results for a period of time.
        /// </para>
        ///
        /// </para>
        /// </remarks>
        public string SessionToken { get; set; }

        /// <summary>
        /// Gets or sets the consistency level required for the request in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The consistency level required for the request.
        /// </value>
        /// <remarks>
        /// Azure Cosmos DB offers 5 different consistency levels. Strong, Bounded Staleness, Session, Consistent Prefix and Eventual - in order of strongest to weakest consistency. <see cref="ConnectionPolicy"/>
        /// <para>
        /// While this is set at a database account level, Azure Cosmos DB allows a developer to override the default consistency level
        /// for each individual request.
        /// </para>
        /// </remarks>
        public ConsistencyLevel? ConsistencyLevel
        {
            get => this.BaseConsistencyLevel;
            set => this.BaseConsistencyLevel = value;
        }

        /// <summary>
        /// Options to encrypt properties of the item.
        /// </summary>
#if PREVIEW
        public
#else
        internal
#endif
        EncryptionOptions EncryptionOptions { get; set; }

        internal bool IsClientEncrypted { get; set; }

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="RequestMessage"/></param>
        internal override void PopulateRequestOptions(RequestMessage request)
        {
            if (this.PreTriggers != null && this.PreTriggers.Any())
            {
                request.Headers.Add(HttpConstants.HttpHeaders.PreTriggerInclude, this.PreTriggers);
            }

            if (this.PostTriggers != null && this.PostTriggers.Any())
            {
                request.Headers.Add(HttpConstants.HttpHeaders.PostTriggerInclude, this.PostTriggers);
            }

            if (this.IndexingDirective != null && this.IndexingDirective.HasValue)
            {
                request.Headers.Add(
                    HttpConstants.HttpHeaders.IndexingDirective,
                    IndexingDirectiveStrings.FromIndexingDirective(this.IndexingDirective.Value));
            }

            if (this.IsClientEncrypted)
            {
                request.Headers.Add(
                    "x-ms-cosmos-is-client-encrypted", // todo: fix post latest .Direct package
                    bool.TrueString);

            }

            RequestOptions.SetSessionToken(request, this.SessionToken);

            base.PopulateRequestOptions(request);
        }
    }
}