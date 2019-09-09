//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Resource token to be used to access resources.
    /// </summary>
    internal sealed class ResourceToken
    {
        /// <summary> 
        /// Gets or sets the self-link of resource to which the token applies.
        /// </summary>
        /// <value>
        /// The self-link of the resource to which the token applies.
        /// </value>
        public string ResourceLink { get; set; }

        /// <summary>
        /// Gets or sets optional partition key value for the token.
        /// A permission applies to resources when two conditions are met:
        ///       1. <see cref="ResourceLink"/> is prefix of resource's link.
        ///             For example "/dbs/mydatabase/colls/mycollection" applies to "/dbs/mydatabase/colls/mycollection" and "/dbs/mydatabase/colls/mycollection/docs/mydocument"
        ///       2. <see cref="ResourcePartitionKey"/> is superset of resource's partition key.
        ///             For example absent/empty partition key is superset of all partition keys.
        /// </summary>
        public object[] ResourcePartitionKey { get; set; }

        /// <summary>
        /// Gets the access token granting the defined permission.
        /// </summary>
        /// <value>
        /// The access token granting the defined permission.
        /// </value>
        public string Token { get; set; }
    }
}
