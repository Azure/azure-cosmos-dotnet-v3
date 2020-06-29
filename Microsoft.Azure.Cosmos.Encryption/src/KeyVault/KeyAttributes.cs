//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// Disable CA1812 to pass the compilation analysis check, as this is only used for deserialization.
#pragma warning disable CA1812
namespace Microsoft.Azure.Cosmos.Encryption
{
    internal sealed class KeyAttributes
    {
        public KeyAttributes(long Created, bool Enabled, long ExpiryDate,
                            long NotBeforeDate, string RecoveryLevel,
                            long Updated)
        {
            this.Created = Created;
            this.Enabled = Enabled;
            this.ExpiryDate = ExpiryDate;
            this.NotBeforeDate = NotBeforeDate;
            this.RecoveryLevel = RecoveryLevel;
            this.Updated = Updated;
        }
        public long Created { get; } // Last updated time in UTC.

        public bool Enabled { get; } // Determines whether the object is enabled.

        public long ExpiryDate { get; } // Expiry date in UTC.

        public long NotBeforeDate { get; } // Not before date in UTC.

        public string RecoveryLevel { get; } // Deletion recovery level currently in effect for keys in the current vault. 

        public long Updated { get; } // Last updated time in UTC.
    }
}