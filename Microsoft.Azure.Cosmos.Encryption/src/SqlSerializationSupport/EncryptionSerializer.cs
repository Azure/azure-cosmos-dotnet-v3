// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <summary>
    /// Encryption Settings for the properties to be encrypted
    /// </summary>
    public abstract class EncryptionSerializer : IEquatable<EncryptionSerializer>
    {
        /// <summary>
        /// Sets the serializer.
        /// </summary>
        /// <param name="serializer"> serializer </param>
        public abstract void SetSerializer(ISerializer serializer);

        /// <summary>
        /// Gets the serializer.
        /// </summary>
        /// <returns> serializer </returns>
        public abstract ISerializer GetSerializer();

        /// <summary>
        /// Creates a copy of Encryption Settings.
        /// </summary>
        /// <param name="encryptionSerializer"> encryptionSerializer </param>
        /// <returns> EncryptionSettings object </returns>
        public static EncryptionSerializer Copy(EncryptionSerializer encryptionSerializer)
        {
            Type genericType = encryptionSerializer.GetType().GenericTypeArguments[0];
            Type settingsType = typeof(EncryptionSerializer<>).MakeGenericType(genericType);
            return (EncryptionSerializer)Activator.CreateInstance(
                settingsType,
                new object[] { encryptionSerializer.GetSerializer() });
        }

        /// <summary>
        /// Create an Encryption Setting with the desired Serialzer.
        /// </summary>
        /// <param name="genericType"> Type </param>
        /// <param name="parameters"> Parameters </param>
        /// <returns> EncryptionSettings </returns>
        public static EncryptionSerializer Create(Type genericType, params object[] parameters)
        {
            Type settingsType = typeof(EncryptionSerializer<>).MakeGenericType(genericType);
            return (EncryptionSerializer)Activator.CreateInstance(settingsType, parameters);
        }

        /// <summary>
        /// Vaidates
        /// </summary>
        /// <param name="other"> Encryption Settings </param>
        /// <returns> returns True or False </returns>
        public bool Equals(EncryptionSerializer other)
        {
            if (other == null)
            {
                return false;
            }

            return this.GetSerializer().GetType().Equals(other.GetSerializer().GetType());
        }

        internal static EncryptionSerializer GetEncryptionSerializer(Type typeName, bool isSqlCompatible = true)
        {
            Type encryptionSerializerClass = typeof(EncryptionSerializer<>).MakeGenericType(typeName);

            object[] args = { isSqlCompatible };

            EncryptionSerializer encryptionSerializer = (EncryptionSerializer)Activator.CreateInstance(encryptionSerializerClass, args);

            return encryptionSerializer;
        }
    }
}
