//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Reflection;

    /// <summary>
    /// This abstract class can be implemented to allow a custom serializer (Non [Json.NET serializer](https://www.newtonsoft.com/json/help/html/Introduction.htm)'s) 
    /// to be used by the CosmosClient for LINQ queries.
    /// </summary>
    /// <remarks>
    /// Refer to the <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos.Samples/Usage/SystemTextJson/CosmosSystemTextJsonSerializer.cs">sample project</see> for a full implementation.
    /// </remarks>
    public abstract class CosmosLinqSerializer : CosmosSerializer
    {
        /// <summary>
        /// Convert a MemberInfo to a string for use in LINQ query translation.
        /// This must be implemented when using a custom serializer for LINQ queries.
        /// </summary>
        /// <param name="memberInfo">Any MemberInfo used in the query.</param>
        /// <returns>A serialized representation of the member.</returns>
        public abstract string SerializeMemberName(MemberInfo memberInfo);
    }
}
