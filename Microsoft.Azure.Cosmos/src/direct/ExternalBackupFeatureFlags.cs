//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using Newtonsoft.Json;

    internal sealed class ExternalBackupFeatureFlags : JsonSerializable, ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalBackupFeatureFlags"/> class.
        /// </summary>
        public ExternalBackupFeatureFlags()
        {
            this.AzureBackupVaultEnabled = false;
        }

        [JsonProperty(PropertyName = Constants.Properties.AzureBackupVaultEnabled)]
        public bool AzureBackupVaultEnabled
        {
            get
            {
                return base.GetValue<bool>(Constants.Properties.AzureBackupVaultEnabled);
            }
            set
            {
                base.SetValue(Constants.Properties.AzureBackupVaultEnabled, value);
            }
        }

        public object Clone()
        {
            return new ExternalBackupFeatureFlags()
            {
                AzureBackupVaultEnabled = this.AzureBackupVaultEnabled,
            };
        }
    }
}
