//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Cosmos batch request options.
    /// </summary>
    public class TransactionalBatchRequestOptions : RequestOptions
    {
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
        /// When working with Session consistency, each batch request with write operation to Azure Cosmos DB is assigned a new SessionToken.
        /// The CosmosClient will use this token internally with each read/query/batch request to ensure that the set
        /// consistency level is maintained.
        ///
        /// <para>
        /// In some scenarios you need to manage this Session yourself;
        /// Consider a web application with multiple nodes, each node will have its own instance of <see cref="CosmosClient"/>
        /// If you wanted these nodes to participate in the same session (to be able read your own writes consistently across web tiers)
        /// you would have to send the SessionToken from <see cref="TransactionalBatchResponse"/> of the write action on one node
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
        /// <see href="https://docs.microsoft.com/azure/cosmos-db/consistency-levels" />
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
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="RequestMessage"/></param>
        internal override void PopulateRequestOptions(RequestMessage request)
        {
            RequestOptions.SetSessionToken(request, this.SessionToken);

            base.PopulateRequestOptions(request);
        }
    }
}
