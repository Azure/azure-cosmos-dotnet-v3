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
        /// Follows the exisitng serializer pattern. This honors Newtonsoft and DataContract attributes, but not System.Text.Json.
        /// </summary>
        Default,

        /// <summary>
        /// Uses a Newtonsoft serializer, which will honor Newtonsoft attributes.
        /// </summary>
        Newtonsoft,

        /// <summary>
        /// Uses a DataContract serializer, which will honor DataMember attributes specified on properies.
        /// </summary>
        DataContract,

        /// <summary>
        /// Uses a custom CosmosSerializer, if provided. This will honor System.Text.Json attributes.
        /// </summary>
        DotNet,
    }
}
