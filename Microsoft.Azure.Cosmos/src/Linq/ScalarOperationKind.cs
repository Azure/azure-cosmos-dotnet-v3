//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    /// <summary>
    /// Represents the operation that needs to be performed on the client side.
    /// </summary>
    /// <remarks>
    /// This enum represents scalar operations such as FirstOrDefault. Scalar operations are disallowed in sub-expressions/sub-queries.
    /// With these restrictations, enum is sufficient, but in future for a larger surface area we may need 
    ///   to use an object model like ClientQL to represent these operations better.
    /// </remarks>
    internal enum ScalarOperationKind
    {
        /// <summary>
        /// Indicates that client does not need to perform any operation on query results.
        /// </summary>
        None,

        /// <summary>
        /// Indicates that the client needs to perform FirstOrDefault on the query results returned by the backend.
        /// </summary>
        FirstOrDefault
    }
}
