//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
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
    class MasterKeyNotFoundException : Exception
    {
        public MasterKeyNotFoundException()
        {
        }

        public MasterKeyNotFoundException(string message)
            : base(message)
        {
        }

        public MasterKeyNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected MasterKeyNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
