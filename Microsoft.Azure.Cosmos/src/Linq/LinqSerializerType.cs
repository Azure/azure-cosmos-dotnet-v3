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
        /// Follows the exisiting serializer pattern. This honors Newtonsoft attributes, followed by DataContract attributes. This will ignore System.Text.Json attributes.
        /// </summary>
        Default,

        /// <summary>
        /// Uses the provided custom CosmosSerializer.
        /// This requires:
        /// 1. a <see cref="CosmosSerializer"/> to be provided on a client, and
        /// 2. the custom CosmosSerializer implements the member function <see cref="CosmosSerializer.SerializeLinqMemberName(System.Reflection.MemberInfo)"/>
        /// </summary>
        CustomCosmosSerializer,
    }
}
