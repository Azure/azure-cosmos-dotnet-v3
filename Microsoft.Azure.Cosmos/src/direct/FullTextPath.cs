//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents the full text path settings for the full text index.
    /// </summary>
    internal sealed class FullTextPath : JsonSerializable
    {
        public FullTextPath()
        {
        }

        /// <summary>
        /// Gets or sets a string containing the path of the full text index.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Path);
            }
            set
            {
                base.SetValue(Constants.Properties.Path, value);
            }
        }

        /// <summary>
        /// Gets or sets a string containing the language of the full text index.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Language)]
        public string Language
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.Language);
            }
            set
            {
                base.SetValue(Constants.Properties.Language, value);
            }
        }
    }
}
