//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.DataEncryptionKeyProvider
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// This exception would be thrown by an <see cref="EncryptionKeyWrapProvider"/> when trying to use
    /// a master key that does not exist. This allows for scenarios where the master key has been rotated
    /// and <see cref="DataEncryptionKey.RewrapAsync"/> has been called to re-wrap the data encryption keys
    /// that were referencing the older version of the master key before removing the old version of the master key
    /// but the client instance is trying to use a cached version of the metadata that references a master key
    /// that no longer exists.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    class KeyNotFoundException : Exception
    {
        /// <summary>
        /// Creates a new instance of master key not found exception.
        /// </summary>
        public KeyNotFoundException()
        {
        }

        /// <summary>
        /// Creates a new instance of master key not found exception with provided message.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        public KeyNotFoundException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Creates a new instance of master key not found exception.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        /// <param name="innerException">Internal exception.</param>
        public KeyNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Creates a new instance of master key not found exception.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        protected KeyNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
