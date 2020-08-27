// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using static Microsoft.Azure.Cosmos.Encryption.SerializerDefaultMappings;

    /// <summary>
    /// Options that allow you to configure how encryption operations are performed on the data of
    /// arbitrary type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The data type on which these encryption settings will apply.</typeparam>
    public class EncryptionSerializer<T> : EncryptionSerializer
    {
        /// <summary>
        /// Gets and Sets Seralizer to be used during Encrytion.
        /// </summary>
        public Serializer<T> Serializer { get; private set; }

        /// <summary>
        /// Encryption Settings,sets the serializer to be used by Encryption with Default serializer
        /// </summary>
        /// <param name="isSqlCompatible"> If Type is Sql Compatible </param>
        public EncryptionSerializer(bool isSqlCompatible)
        {
            this.Serializer = isSqlCompatible ? GetDefaultSqlSerializer<T>() : GetDefaultSerializer<T>();
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
