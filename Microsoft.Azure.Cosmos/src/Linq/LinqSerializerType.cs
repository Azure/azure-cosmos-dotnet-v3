//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    /// <summary>
    /// Serializer type to be used for LINQ query translations.
    /// </summary>
    public enum LinqSerializerType
    {
        /// <summary>
        /// Follows the exisiting serializer pattern. This honors Newtonsoft attributes, followed by DataContract attributes, but not System.Text.Json attributes.
        /// </summary>
        Default,

        /// <summary>
        /// Uses a custom CosmosSerializer, if provided. This will honor System.Text.Json attributes.
        /// </summary>
        CustomCosmosSerializer,
    }
}
