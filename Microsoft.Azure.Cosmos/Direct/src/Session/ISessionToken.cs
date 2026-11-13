//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;

    /// <summary>
    /// Models session token. There are two implementations of this interface:
    /// 1. <see cref="VectorSessionToken"/> available for clients with version <see cref="HttpConstants.Versions.v2018_06_18"/> onwards
    /// 2. <see cref="SimpleSessionToken"/> available for clients with versions before <see cref="HttpConstants.Versions.v2018_06_18"/>
    /// Internal format/implementation should be opaque to its caller.
    ///
    /// We make assumption that instances of this interface are immutable (read only after they are constructed), so if you want to change
    /// this behaviour please review all of its uses and make sure that mutability doesn't break anything.
    /// </summary>
    internal interface ISessionToken : IEquatable<ISessionToken>
    {
        /// <summary>
        /// Returns true if this instance of session token is valid with respect to <paramref name="other"/> session token.
        /// This is used to decide if the the client can accept server's response (based on comparison between client's 
        /// and server's session token)
        /// </summary>
        /// <param name="other">Session token to validate</param>
        /// <returns>true if this instance of session  token is valid with respect to <paramref name="other"/> session token;
        /// false otherwise</returns>
        bool IsValid(ISessionToken other);

        /// <summary>
        /// Returns a new instance of session token obtained by merging this session token with the given session token <paramref name="other"/>.
        ///
        /// Merge is commutative operation, so a.Merge(b).Equals(b.Merge(a))
        /// </summary>
        /// <param name="other">Other session token to merge</param>
        /// <returns>Instance of merged session token</returns>
        ISessionToken Merge(ISessionToken other);

        long LSN { get; }

        string ConvertToString();
    }
}
