//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents the Container Level Client Encryption Policy.
    /// Provide metadata info on each of the property that will be encrypted.
    /// </summary>
    public sealed class ClientEncryptionPolicy
    {
        /// <summary>
        /// Gets and Sets Encryption Setting for the Property
        /// </summary>
        public List<KeyValuePair<List<string>, PropertyEncryptionSetting>> ClientEncryptionSetting { get; set; } = new List<KeyValuePair<List<string>, PropertyEncryptionSetting>>();

        /// <summary>
        /// Register a Client Encryption Policy
        /// </summary>
        /// <param name="propertiesToEncrypt"> Property Names to Encrypt </param>
        /// <param name="propertyEncryptionSetting"> Property Settings </param>
        public void RegisterClientEncryptionPolicy(List<string> propertiesToEncrypt, PropertyEncryptionSetting propertyEncryptionSetting)
        {
            this.ClientEncryptionSetting.Add(new KeyValuePair<List<string>, PropertyEncryptionSetting>(propertiesToEncrypt, propertyEncryptionSetting));
        }
    }

    /// <summary>
    /// Property specific Encryption Meta Data
    /// </summary>
    public class PropertyEncryptionSetting
    {
        /// <summary>
        /// Gets or Sets Encryption Format Version
        /// </summary>
        public int EncryptionFormatVersion { get; set; }

        /// <summary>
        /// Gets or Sets the Data Type of the value to be encrypted.
        /// </summary>
        public Type PropertyDataType { get; set; }

        /// <summary>
        /// Gets or sets the Data Encryption Key Id
        /// </summary>
        public string DataEncryptionKeyId { get; set; }

        /// <summary>
        /// Gets or Sets the Encryption Algorithm
        /// </summary>
        public string EncryptionAlgorithm { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Property Type is Sql Compatible
        /// </summary>
        public bool IsSqlCompatible { get; set; }

        /// <summary>
        /// Sets the Property Encryption Meta Info
        /// </summary>
        /// <param name="dataEncryptionKeyId"> Data Encryption Key Id </param>
        /// <param name="encryptionFormatVersion"> Encryption Format version </param>
        /// <param name="propertyDataType"> Encrypted Parameter Data Type </param>
        /// <param name="encryptionAlgorithm"> Encryption Algorithm </param>
        /// <param name="isSqlCompatible"> Property Sql Compatibility </param>
        public PropertyEncryptionSetting(string dataEncryptionKeyId, int encryptionFormatVersion, Type propertyDataType, string encryptionAlgorithm, bool isSqlCompatible)
        {
            this.EncryptionFormatVersion = encryptionFormatVersion;
            this.DataEncryptionKeyId = dataEncryptionKeyId;
            this.PropertyDataType = propertyDataType;
            this.EncryptionAlgorithm = encryptionAlgorithm;
            this.IsSqlCompatible = isSqlCompatible;
        }
    }
}