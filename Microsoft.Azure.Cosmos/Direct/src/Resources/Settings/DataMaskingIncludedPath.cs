//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;

    internal sealed class DataMaskingIncludedPath : JsonSerializable
    {
        /// <summary>
        /// Gets or sets the path to be masked. Must be a top level path, eg. /salary
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path
        {
            get { return this.GetValue<string>(Constants.Properties.Path); }
            set { this.SetValue(Constants.Properties.Path, value); }
        }
    }
}
