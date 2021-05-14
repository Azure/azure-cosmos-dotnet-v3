//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos container request options
    /// </summary>
    public class ContainerRequestOptions : RequestOptions
    {
        /// <summary>
        ///  Gets or sets the <see cref="PopulateQuotaInfo"/> for container read requests in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// PopulateQuotaInfo is used to enable/disable getting container quota related stats for container read requests.
        /// </para>
        /// </remarks>
        public bool PopulateQuotaInfo { get; set; }

        /// <summary>
        ///  Gets or sets the <see cref="PopulateAnalyticalMigrationProgress"/> for container read requests in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// PopulateAnalyticalMigrationProgress is used to enable/disable getting analytical migration progress for container read requests.
        /// The response header "x-ms-cosmos-analytical-migration-progress" will contain an integer representing the percentage progress of analytical migration. 
        /// The response header will be between 0 and 99 for containers that are undergoing analytical migration, 100 for containers that have finished migration, and -1 otherwise.
        /// </para>
        /// </remarks>
#if PREVIEW
        public
#else
        internal
#endif
        bool PopulateAnalyticalMigrationProgress { get; set; }

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="RequestMessage"/></param>
        internal override void PopulateRequestOptions(RequestMessage request)
        {
            if (this.PopulateQuotaInfo)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.PopulateQuotaInfo, bool.TrueString);
            }

            if (this.PopulateAnalyticalMigrationProgress)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.PopulateAnalyticalMigrationProgress, bool.TrueString);
            }

            base.PopulateRequestOptions(request);
        }
    }
}