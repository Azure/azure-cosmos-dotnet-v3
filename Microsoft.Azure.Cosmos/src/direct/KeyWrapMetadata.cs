namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;

    /// <summary>
    /// Represents key wrap metadata associated with a client encryption key.
    /// </summary>
    internal class KeyWrapMetadata : JsonSerializable
    {
        public KeyWrapMetadata()
        {

        }

        [JsonProperty(PropertyName = Constants.Properties.KeyWrapMetadataName, NullValueHandling = NullValueHandling.Ignore)]
        internal string Name
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.KeyWrapMetadataName);
            }
            set
            {
                base.SetValue(Constants.Properties.KeyWrapMetadataName, value);
            }
        }
        
        [JsonProperty(PropertyName = Constants.Properties.KeyWrapMetadataType, NullValueHandling = NullValueHandling.Ignore)]
        internal string Type
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.KeyWrapMetadataType);
            }
            set
            {
                base.SetValue(Constants.Properties.KeyWrapMetadataType, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.KeyWrapMetadataValue, NullValueHandling = NullValueHandling.Ignore)]
        internal string Value
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.KeyWrapMetadataValue);
            }
            set
            {
                base.SetValue(Constants.Properties.KeyWrapMetadataValue, value);
            }
        }
    }
}
