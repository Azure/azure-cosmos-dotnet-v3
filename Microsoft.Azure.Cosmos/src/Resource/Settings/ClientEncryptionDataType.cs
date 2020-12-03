//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// This determines the correct MicrosoftDataEncryption (MDE) Serializer to map to corresponding to the data type.
    /// </summary>
#if PREVIEW
    public 
#else
    internal
#endif
        enum ClientEncryptionDataType
    {
        /// <summary>
        /// MDE Bool serializer will be used
        /// </summary>
        Bool,

        /// <summary>
        /// MDE Double serializer will be used
        /// </summary>
        Double,

        /// <summary>
        /// MDE Long serializer will be used
        /// </summary>
        Long,
        
        /// <summary>
        /// MDE String serializer will be used
        /// </summary>
        String
    }
}
