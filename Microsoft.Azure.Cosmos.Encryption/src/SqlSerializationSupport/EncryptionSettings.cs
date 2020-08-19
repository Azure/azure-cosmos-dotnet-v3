// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <summary>
    /// Encryption Settings for the properties to be encrypted
    /// </summary>
    public abstract class EncryptionSettings : IEquatable<EncryptionSettings>
    {
        /// <summary>
        /// Gets or Sets Encryption Format Version
        /// </summary>
        public int EncryptionFormatVersion { get; set; }

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
        /// Creates a copy of Encryption Settings.
        /// </summary>
        /// <param name="encryptionSettings"> encryptionSettings </param>
        /// <returns> EncryptionSettings object </returns>
        public static EncryptionSettings Copy(EncryptionSettings encryptionSettings)
        {
            Type genericType = encryptionSettings.GetType().GenericTypeArguments[0];
            Type settingsType = typeof(EncryptionSettings<>).MakeGenericType(genericType);
            return (EncryptionSettings)Activator.CreateInstance(
                settingsType,
                new object[] { encryptionSettings.EncryptionFormatVersion, encryptionSettings.PropertyDataType, encryptionSettings.DataEncryptionKeyId, encryptionSettings.EncryptionAlgorithm, encryptionSettings.GetSerializer() });
        }

        /// <summary>
        /// Create an Encryption Setting with the desired Serialzer.
        /// </summary>
        /// <param name="genericType"> Type </param>
        /// <param name="parameters"> Parameters </param>
        /// <returns> EncryptionSettings </returns>
        public static EncryptionSettings Create(Type genericType, params object[] parameters)
        {
            Type settingsType = typeof(EncryptionSettings<>).MakeGenericType(genericType);
            return (EncryptionSettings)Activator.CreateInstance(settingsType, parameters);
        }

        /// <summary>
        /// Vaidates
        /// </summary>
        /// <param name="other"> Encryption Settings </param>
        /// <returns> returns True or False </returns>
        public bool Equals(EncryptionSettings other)
        {
            if (other == null)
            {
                return false;
            }

            return this.GetSerializer().GetType().Equals(other.GetSerializer().GetType());
        }
    }
}
