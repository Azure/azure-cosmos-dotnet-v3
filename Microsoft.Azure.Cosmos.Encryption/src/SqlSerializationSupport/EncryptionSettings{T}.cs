// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using static Microsoft.Azure.Cosmos.Encryption.SerializerDefaultMappings;

    /// <summary>
    /// Options that allow you to configure how encryption operations are performed on the data of
    /// arbitrary type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The data type on which these encryption settings will apply.</typeparam>
    public class EncryptionSettings<T> : EncryptionSettings
    {
        /// <summary>
        /// Gets and Sets Seralizer to be used during Encrytion.
        /// </summary>
        public Serializer<T> Serializer { get; private set; }

        /// <summary>
        /// Encryption Settings,sets the serializer to be used by Encryption with Default serializer
        /// </summary>
        /// <param name="dataEncryptionKeyId"> dataEncryptionKeyId </param>
        /// <param name="encryptionAlgorithm"> Encryption Algorithm Default: AEADAes256CbcHmacSha256Deterministic </param>
        /// <param name="isSqlCompatible"> isSqlCompatible </param>
        public EncryptionSettings(string dataEncryptionKeyId, string encryptionAlgorithm = CosmosEncryptionAlgorithm.AEADAes256CbcHmacSha256Deterministic, bool isSqlCompatible = true)
            : this(dataEncryptionKeyId, isSqlCompatible ? GetDefaultSqlSerializer<T>() : GetDefaultSerializer<T>(), encryptionAlgorithm: encryptionAlgorithm)
        {
        }

        /// <summary>
        /// Encryption Settings,sets the serializer to be used by Encryption.
        /// </summary>
        /// <param name="dataEncryptionKeyId"> DEK Id </param>
        /// <param name="dataType"> Data Type </param>
        /// <param name="serializer"> serializer </param>
        /// <param name="encryptionAlgorithm"> Encryption Algorithm </param>
        public EncryptionSettings(string dataEncryptionKeyId, Serializer<T> serializer, string encryptionAlgorithm)
        {
            this.DataEncryptionKeyId = dataEncryptionKeyId;
            this.PropertyDataType = typeof(T);
            this.EncryptionAlgorithm = encryptionAlgorithm;
            this.Serializer = serializer;
        }

        /// <summary>
        /// Sets the serializer.
        /// </summary>
        /// <param name="serializer"> serializer </param>
        public override void SetSerializer(ISerializer serializer)
        {
            serializer.ValidateType(typeof(Serializer<T>), nameof(serializer));
            this.Serializer = (Serializer<T>)serializer;
        }

        /// <summary>
        /// Gets the serializer.
        /// </summary>
        /// <returns> serializer </returns>
        public override ISerializer GetSerializer()
        {
            return this.Serializer;
        }
    }
}
