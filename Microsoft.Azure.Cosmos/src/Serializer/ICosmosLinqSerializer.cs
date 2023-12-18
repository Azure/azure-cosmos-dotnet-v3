//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// This interface can be implemented to allow a custom serializer to be used by the CosmosClient
    /// for both CRUD operations and LINQ queries.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    interface ICosmosLinqSerializer
    {
        /// <summary>
        /// Convert a <see cref="MemberInfo"/> to a string for use in LINQ query translation.
        /// This must be implemented when using a custom serializer for LINQ queries.
        /// </summary>
        /// <param name="memberInfo">Any MemberInfo used in the query.</param>
        /// <returns>A serialized representation of the member.</returns>
        public string SerializeLinqMemberName(MemberInfo memberInfo);

        /// <summary>
        /// <see cref="CosmosSerializer.FromStream{T}(Stream)"/>
        /// </summary>
        public T FromStream<T>(Stream stream);

        /// <summary>
        /// <see cref="CosmosSerializer.ToStream{T}(T)"/>
        /// </summary>
        public Stream ToStream<T>(T input);
    }
}
