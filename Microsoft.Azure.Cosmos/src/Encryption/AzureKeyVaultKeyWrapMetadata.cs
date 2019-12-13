//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Metadata used by Azure Key Vault to wrap (encrypt) and unwrap (decrypt) keys.
    /// </summary>
    public class AzureKeyVaultKeyWrapMetadata : KeyWrapMetadata
    {
        private string masterKeyUrl;

        internal override string Type
        {
            get
            {
                return "akv";
            }
        }

        /// <inheritdoc/>
        public override string Value
        {
            get
            {
                return this.masterKeyUrl;
            }
        }

        /// <summary>
        /// Creates a new instance of metadata that the Azure Key Vault can use to wrap and unwrap keys.
        /// </summary>
        /// <param name="masterKeyUrl">Key Vault URL of the master key to be used for wrapping and unwrapping keys.</param>
        public AzureKeyVaultKeyWrapMetadata(string masterKeyUrl)
        {
            this.masterKeyUrl = masterKeyUrl;
        }
    }
}
