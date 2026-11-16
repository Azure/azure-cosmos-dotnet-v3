//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Linq;

    /// <summary>
    /// Resource representing an Azure RBAC resource for a Cosmos DB account.
    /// It can hold data for an Azure RBAC role assignment or role definition.
    /// </summary>
    internal sealed class AzureRbac : Resource
    {

        public AzureRbac()
        {
        }

        public AzureRbac(
            string id,
            byte[] data)
        {
            this.Id = id;
            this.Data = data;
        }

        /// <summary>
        /// Data for the Azure RBAC resource.
        /// </summary>
        public byte[] Data
        {
            get
            {
                byte[] dataValue = base.GetValue<byte[]>(AzureRbac.SerializationConstants.Data);

                if (dataValue == null || dataValue.Length == 0)
                {
                    throw new FormatException(
                        string.Format(
                            AzureRbac.SerializationErrors.MissingRequiredProperty,
                            AzureRbac.SerializationConstants.Data));
                }

                return dataValue;
            }
            internal set
            {
                // Store as Base64 string to ensure consistency between in-memory and deserialized representations.
                // When a byte[] is stored via JToken.FromObject(), it creates a JTokenType.Bytes token.
                // However, after JSON serialization and deserialization, it becomes a JTokenType.String (Base64).
                // This mismatch causes checkpoint verification failures in OperationStateManager.
                // By explicitly storing as Base64 string, both representations will match.
                base.SetValue(AzureRbac.SerializationConstants.Data, value != null ? Convert.ToBase64String(value) : null);
            }
        }

        internal override void Validate()
        {
            base.Validate();

            if (string.IsNullOrWhiteSpace(this.Id))
            {
                throw new FormatException(
                    string.Format(
                        AzureRbac.SerializationErrors.MissingRequiredProperty,
                        Constants.Properties.Id));
            }

            _ = this.Data;
        }

        public static bool AreEquivalent(AzureRbac a, AzureRbac b)
        {
            if (a == null || b == null)
            {
                return a == null && b == null;
            }

            if (!string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (a.Data == null || b.Data == null)
            {
                return a.Data == null && b.Data == null;
            }

            return a.Data.SequenceEqual(b.Data);
        }

        private static class SerializationConstants
        {
            public const string Data = "data";
        }

        internal static class SerializationErrors
        {
            public const string MissingRequiredProperty = "Required property [{0}] is not present or is empty.";
        }
    }
}