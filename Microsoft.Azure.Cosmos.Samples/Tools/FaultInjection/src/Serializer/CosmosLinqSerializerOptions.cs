// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// This class provides a way to configure Linq Serialization Properties
    /// </summary>
    public sealed class CosmosLinqSerializerOptions
    {
        /// <summary>
        /// Create an instance of CosmosSerializationOptions
        /// with the default values for the Cosmos SDK
        /// </summary>
        public CosmosLinqSerializerOptions()
        {
            this.PropertyNamingPolicy = CosmosPropertyNamingPolicy.Default;
        }

        /// <summary>
        /// Gets or sets whether the naming policy used to convert a string-based name to another format,
        /// such as a camel-casing format.
        /// </summary>
        /// <remarks>
        /// The default value is CosmosPropertyNamingPolicy.Default
        /// </remarks>
        public CosmosPropertyNamingPolicy PropertyNamingPolicy { get; set; }
    }
}
