//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents the client encryption policy for a collection in the Azure Cosmos DB service.
    /// </summary>
    /// <seealso cref="Microsoft.Azure.Documents.DocumentCollection"/>
    internal sealed class ClientEncryptionPolicy : JsonSerializable
    {
        private Collection<ClientEncryptionIncludedPath> includedPaths;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientEncryptionPolicy"/> class for the Azure Cosmos DB service.
        /// </summary>
        public ClientEncryptionPolicy()
        {
        }

        /// <summary>
        /// Paths of the item that need encryption along with path-specific settings.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.IncludedPaths)]
        public Collection<ClientEncryptionIncludedPath> IncludedPaths
        {
            get
            {
                if (this.includedPaths == null)
                {
                    this.includedPaths = base.GetObjectCollection<ClientEncryptionIncludedPath>(Constants.Properties.IncludedPaths);
                    if (this.includedPaths == null)
                    {
                        this.includedPaths = new Collection<ClientEncryptionIncludedPath>();
                    }
                }

                return this.includedPaths;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, nameof(IncludedPaths)));
                }

                this.includedPaths = value;
                base.SetObjectCollection(Constants.Properties.IncludedPaths, this.includedPaths);
            }
        }

        internal override void OnSave()
        {
            if (this.includedPaths != null)
            {
                base.SetObjectCollection(Constants.Properties.IncludedPaths, this.includedPaths);
            }
        }
    }
}
