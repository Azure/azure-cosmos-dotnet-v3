//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Serializer type to be used for LINQ query translations.
    /// </summary>
#if PREVIEW
    public
#else
    internal
    #endif
    enum CosmosLinqSerializerType
    {
        /// <summary>
        /// This honors Newtonsoft attributes, followed by DataContract attributes. This will ignore System.Text.Json attributes.
        /// </summary>
        Default,

        /// <summary>
        /// Uses the provided custom CosmosSerializer.
        /// This requires:
        /// 1. a <see cref="CosmosSerializer"/> to be provided on a client, and
        /// 2. the custom CosmosSerializer implements <see cref="ICosmosLinqSerializer"/>
        /// </summary>
        CustomCosmosSerializer,
    }
}
