// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Linq;

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
            this.LinqSerializerType = CosmosLinqSerializerType.Default;
        }

        /// <summary>
        /// Gets or sets whether the naming policy used to convert a string-based name to another format,
        /// such as a camel-casing format.
        /// </summary>
        /// <remarks>
        /// The default value is CosmosPropertyNamingPolicy.Default
        /// </remarks>
        public CosmosPropertyNamingPolicy PropertyNamingPolicy { get; set; }

        /// <summary>
        /// Specifies the type of serializer to be used for LINQ translations.
        /// Options are detailed in <see cref="LinqSerializerType"/>
        /// </summary>
        /// <remarks>
        /// The default value is CosmosLinqSerializerType.Default
        /// </remarks>
        #if PREVIEW
        public
        #else
        internal
        #endif
        CosmosLinqSerializerType LinqSerializerType { get; set; }
    }
}
