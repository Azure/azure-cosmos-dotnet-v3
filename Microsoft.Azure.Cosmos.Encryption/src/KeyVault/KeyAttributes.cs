//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// Disable CA1812 to pass the compilation analysis check, as this is only used for deserialization.
#pragma warning disable CA1812
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    internal sealed class KeyProperties
    {
        public KeyProperties(DateTimeOffset CreatedOn, bool Enabled, DateTimeOffset ExpiresOn,
                            DateTimeOffset NotBefore, string RecoveryLevel,
                            DateTimeOffset UpdatedOn)
        {
            this.CreatedOn = CreatedOn;
            this.Enabled = Enabled;
            this.ExpiresOn = ExpiresOn;
            this.NotBefore = NotBefore;
            this.RecoveryLevel = RecoveryLevel;
            this.UpdatedOn = UpdatedOn;
        }
        //
        // Summary:
        //     Gets the key identifier.
        public Uri Id { get; }
        //
        // Summary:
        //     Gets the Key Vault base System.Uri.
        public Uri VaultUri { get; }
        //
        // Summary:
        //     Gets the version of the key.
        public string Version { get; }
        //
        // Summary:
        //     Gets a value indicating whether the key's lifetime is managed by Key Vault. If
        //     this key is backing a Key Vault certificate, the value will be true.
        public bool Managed { get; }
        //
        // Summary:
        //     Gets a dictionary of tags with specific metadata about the key.
        public IDictionary<string, string> Tags { get; }
        //
        // Summary:
        //     Gets or sets a value indicating whether the key is enabled and useable for cryptographic
        //     operations.
        public bool? Enabled { get; set; }
        //
        // Summary:
        //     Gets or sets a System.DateTimeOffset indicating when the key will be valid and
        //     can be used for cryptographic operations.
        public DateTimeOffset? NotBefore { get; set; }
        //
        // Summary:
        //     Gets or sets a System.DateTimeOffset indicating when the key will expire and
        //     cannot be used for cryptographic operations.
        public DateTimeOffset? ExpiresOn { get; set; }
        //
        // Summary:
        //     Gets a System.DateTimeOffset indicating when the key was created.
        public DateTimeOffset? CreatedOn { get; }
        //
        // Summary:
        //     Gets a System.DateTimeOffset indicating when the key was updated.
        public DateTimeOffset? UpdatedOn { get; }
        //
        // Summary:
        //     Gets the recovery level currently in effect for keys in the Key Vault. If Purgeable,
        //     the key can be permanently deleted by an authorized user; otherwise, only the
        //     service can purge the keys at the end of the retention interval.
        //
        // Value:
        //     Possible values include Purgeable, Recoverable+Purgeable, Recoverable, and Recoverable+ProtectedSubscription.
        public string RecoveryLevel { get; }
    }
}