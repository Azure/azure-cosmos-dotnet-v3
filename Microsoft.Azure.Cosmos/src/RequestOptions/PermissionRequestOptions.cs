//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos container request options
    /// </summary>
    public class PermissionRequestOptions : RequestOptions
    {
        /// <summary>
        /// Gets or sets the expiry time for resource token. Used when creating/updating/reading permissions in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The expiry time in seconds for the resource token.
        /// </value>
        /// <remarks>
        /// When working with Azure Cosmos DB Users and Permissions, the way to instantiate an instance of <see cref="Microsoft.Azure.Cosmos.CosmosClient"/> is to
        /// get the <see cref="Permission.Token"/> for the resource the <see cref="User"/> wants to access and pass this
        /// to the authKeyOrResourceToken parameter of <see cref="Microsoft.Azure.Cosmos.CosmosClient"/> constructor
        /// <para>
        /// When requesting this Token, a RequestOption for ResourceTokenExpirySeconds can be used to set the length of time to elapse before the token expires.
        /// This value can range from 10 seconds, to 5 hours (or 18,000 seconds)
        /// The default value for this, should none be supplied is 1 hour (or 3,600 seconds).
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Cosmos.CosmosClient"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.PermissionProperties"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.UserProperties"/>
        public int? ResourceTokenExpirySeconds { get; set; }

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="RequestMessage"/></param>
        internal override void PopulateRequestOptions(RequestMessage request)
        {
            if (this.ResourceTokenExpirySeconds != null)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.ResourceTokenExpiry, this.ResourceTokenExpirySeconds.ToString());
            }

            base.PopulateRequestOptions(request);
        }
    }
}
