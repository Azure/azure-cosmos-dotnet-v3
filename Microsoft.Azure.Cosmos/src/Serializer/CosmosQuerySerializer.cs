//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Reflection;

    /// <summary>
    /// This abstract class can be implemented to allow a custom serializer to be used by the CosmosClient
    /// for both CRUD operations and LINQ queries.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    abstract class CosmosQuerySerializer : CosmosSerializer
    {
        /// <summary>
        /// Convert a MemberInfo to a string for use in LINQ query translation.
        /// This must be implemented when using a custom serializer for LINQ queries.
        /// </summary>
        /// <param name="memberInfo">Any MemberInfo used in the query.</param>
        /// <returns>A serialized representation of the member.</returns>
        public abstract string SerializeLinqMemberName(MemberInfo memberInfo);
    }
}
