//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Admin
{
    using System.Runtime.Serialization;
    using Microsoft.Azure.Cosmos.Internal;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    internal sealed class ModuleCommand : CosmosResource
    {
        [JsonProperty(PropertyName = Constants.Properties.ModuleEvent)]
        public ModuleEvent Event
        {
            get
            {
                return base.GetValue<ModuleEvent>(Constants.Properties.ModuleEvent);
            }
            set
            {
                base.SetValue(Constants.Properties.ModuleEvent, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.ModuleEventReason)]
        public int ModuleEventReason
        {
            get
            {
                return base.GetValue<int>(Constants.Properties.ModuleEventReason);
            }
            set
            {
                base.SetValue(Constants.Properties.ModuleEventReason, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.Result)]
        public int Result
        {
            get
            {
                return base.GetValue<int>(Constants.Properties.Result);
            }
        }
    }
}
