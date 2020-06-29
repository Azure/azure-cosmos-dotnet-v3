//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// Disable CA1812 to pass the compilation analysis check, as this is only used for deserialization.
#pragma warning disable CA1812
namespace Microsoft.Azure.Cosmos.Encryption
{
    internal sealed class KeyAttributes
    {
        public long Created { get; set; } // Last updated time in UTC.

        public bool Enabled { get; set; } // Determines whether the object is enabled.

        public long Exp { get; set; } // Expiry date in UTC.

        public long Nbf { get; set; } // Not before date in UTC.

        public string RecoveryLevel { get; set; } // Deletion recovery level currently in effect for keys in the current vault. 

        public long Updated { get; set; } // Last updated time in UTC.
    }
}