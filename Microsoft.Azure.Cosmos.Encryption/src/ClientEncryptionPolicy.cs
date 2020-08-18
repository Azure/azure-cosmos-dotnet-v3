//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.Collections.Generic;

    /// <summary>
    /// Client Side Encryption Policy Settings.
    /// </summary>
    public class ClientEncryptionPolicy
    {
        /// <summary>
        /// Gets and Sets Encryption Setting for the Property
        /// </summary>
        internal Dictionary<List<string>, EncryptionSettings> PropertyEncryptionSetting { get; } = new Dictionary<List<string>, EncryptionSettings>();

        /// <summary>
        /// Register a Client Encryption Policy
        /// </summary>
        /// <param name="propertiesToEncrypt"> Property Names to Encrypt </param>
        /// <param name="encryptionSettings"> Encryption Settings </param>
        public void RegisterClientEncryptionPolicy(List<string> propertiesToEncrypt, EncryptionSettings encryptionSettings)
        {
            this.PropertyEncryptionSetting[propertiesToEncrypt] = encryptionSettings;
        }
    }
}