//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos container request options
    /// </summary>
    public class CosmosContainerRequestOptions : RequestOptions
    {
        /// <summary>
        ///  Gets or sets the <see cref="PopulateQuotaInfo"/> for document collection read requests in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// PopulateQuotaInfo is used to enable/disable getting document collection quota related stats for document collection read requests.
        /// </para>
        /// </remarks>
        public virtual bool PopulateQuotaInfo { get; set; }

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="CosmosRequestMessage"/></param>
        public override void FillRequestOptions(CosmosRequestMessage request)
        {
            if (this.PopulateQuotaInfo)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.PopulateQuotaInfo, bool.TrueString);
            }

            base.FillRequestOptions(request);
        }
    }
}