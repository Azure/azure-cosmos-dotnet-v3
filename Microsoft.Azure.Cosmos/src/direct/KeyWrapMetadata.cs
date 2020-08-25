using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents key wrap metadata associated with a client encryption key.
    /// </summary>
    internal class KeyWrapMetadata : JsonSerializable
    {
        public KeyWrapMetadata()
        {

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
