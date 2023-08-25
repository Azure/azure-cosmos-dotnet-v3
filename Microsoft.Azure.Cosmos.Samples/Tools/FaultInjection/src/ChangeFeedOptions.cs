//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Specifies the options associated with change feed methods (enumeration operations) in the Azure Cosmos DB service.
    /// </summary>
    internal sealed class ChangeFeedOptions
    {
        private DateTime? startTime;

        /// <summary>
        /// Gets or sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum number of items to be returned in the enumeration operation.
        /// </value> 
        /// <remarks>
        /// Used for query pagination.
        /// '-1' Used for dynamic page size.
        /// </remarks>
        public int? MaxItemCount { get; set; }

        /// <summary>
        /// Gets or sets the request continuation token in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request continuation token.
        /// </value>
        public string RequestContinuation { get; set; }

        /// <summary>
        /// Gets or sets the session token for use with session consistency in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The session token for use with session consistency.
        /// </value>
        /// <remarks>
        /// Useful for applications that are load balanced across multiple Microsoft.Azure.Documents.Client.DocumentClient instances. 
        /// In this case, round-trip the token from end user to the application and then back to Azure Cosmos DB so that a session
        /// can be preserved across servers.
        /// </remarks>
        public string SessionToken { get; set; }

        /// <summary>
        /// Gets or sets the partition key range id for the current request in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// ChangeFeed requests can be executed against specific partition key ranges. 
        /// This is used to process the change feed in parallel across multiple consumers.
        /// PartitionKeyRangeId cannot be specified along with PartitionKey.
        /// </remarks>
        /// <see cref="Microsoft.Azure.Documents.PartitionKeyRange" />
        /// <see cref="DocumentClient.ReadPartitionKeyRangeFeedAsync(string, FeedOptions)"/>
        public string PartitionKeyRangeId { get; set; }

        /// <summary>
        /// Gets or sets the partition key for the current request in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// PartitionKey cannot be specified along with PartitionKeyRangeId.
        /// </remarks>
        /// <see cref="Microsoft.Azure.Documents.PartitionKey" />
        /// <example>
        /// <![CDATA[
        /// var options = new ChangeFeedOptions()
        /// {
        ///     PartitionKey = new PartitionKey("c7580115-8f46-4ac4-a0c7-22eae9aaabf1"),
        ///     StartFromBeginning = true
        /// };
        /// ]]>
        /// </example>
        public Documents.PartitionKey PartitionKey { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether change feed in the Azure Cosmos DB service 
        /// should start from beginning (true) or from current (false).
        /// By default it's start from current (false).
        /// </summary>
        public bool StartFromBeginning { get; set; }

        /// <summary>
        /// Gets or sets the time (exclusive) to start looking for changes after.
        /// If this is specified, StartFromBeginning is ignored.
        /// </summary>
        public DateTime? StartTime
        {
            get => this.startTime;

            set
            {
                if (value.HasValue && value.Value.Kind == DateTimeKind.Unspecified)
                {
                    throw new ArgumentException(RMResources.ChangeFeedOptionsStartTimeWithUnspecifiedDateTimeKind, "value");
                }

                this.startTime = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether change feed in the Azure Cosmos DB service 
        /// should return tentative writes in addition to committed writes.
        /// By default the flag is set to false meaning only committed writes will be sent in response.
        /// </summary>
        internal bool IncludeTentativeWrites { get; set; }
    }
}
