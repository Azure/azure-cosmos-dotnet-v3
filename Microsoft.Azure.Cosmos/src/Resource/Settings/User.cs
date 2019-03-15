//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Internal
{
    /// <summary> 
    /// Represents a user in the Azure Cosmos DB service.
    /// </summary>
    internal class User : CosmosResource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="User"/> class for the Azure Cosmos DB service.
        /// </summary>
        public User()
        {

        }

        /// <summary>
        /// Gets the self-link of the permissions associated with the user for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The self-link of the permissions associated with the user.</value> 

        public string PermissionsLink
        {
            get
            {
                return this.SelfLink.TrimEnd('/') + "/" + base.GetValue<string>(Constants.Properties.PermissionsLink);
            }
        }
    }
}
