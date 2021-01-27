//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;

    internal sealed class UniqueIndexReIndexContext : JsonSerializable
    {
        private Collection<UniqueKey> uniqueKeys;
        
        [JsonProperty(PropertyName = Constants.Properties.UniqueKeys)]
        public Collection<UniqueKey> UniqueKeys
        {
            get
            {
                if (this.uniqueKeys == null)
                {
                    this.uniqueKeys = base.GetValue<Collection<UniqueKey>>(Constants.Properties.UniqueKeys);
                    if (this.uniqueKeys == null)
                    {
                        this.uniqueKeys = new Collection<UniqueKey>();
                    }
                }

                return this.uniqueKeys;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, RMResources.PropertyCannotBeNull, "UniqueKeys"));
                }

                this.uniqueKeys = value;
                base.SetValue(Constants.Properties.UniqueKeys, this.uniqueKeys);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.LastDocumentGLSN)]
        public ulong LastDocumentGLSN
        {
            get 
            { 
                return base.GetValue<ulong>(Constants.Properties.LastDocumentGLSN); 
            }
            set 
            {
                base.SetValue(Constants.Properties.LastDocumentGLSN, value);
            }
        }

        internal override void OnSave()
        {
            if (this.uniqueKeys != null)
            {
                foreach (UniqueKey uniqueKey in this.uniqueKeys)
                {
                    uniqueKey.OnSave();
                }

                base.SetValue(Constants.Properties.UniqueKeys, this.uniqueKeys);
            }
        }

        internal override void Validate()
        {
            base.Validate();
            foreach (UniqueKey uniqueKey in this.UniqueKeys)
            {
                uniqueKey.Validate();
            }
        }
    }
}
