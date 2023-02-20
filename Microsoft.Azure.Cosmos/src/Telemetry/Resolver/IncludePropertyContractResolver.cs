//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Resolver
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    internal class IncludePropertyContractResolver : DefaultContractResolver
    {
        private static readonly HashSet<string> propertiesToIgnore = ClientTelemetryOptions.PropertiesContainMetrics;
        
        private readonly string includeProp;
        public IncludePropertyContractResolver(string propNameToInclude)
        {
            this.includeProp = propNameToInclude;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);
            
            if (IncludePropertyContractResolver.propertiesToIgnore.Contains(property.PropertyName) 
                && !string.Equals(this.includeProp, property.PropertyName, StringComparison.OrdinalIgnoreCase))
            {
                property.ShouldSerialize = _ => false;
            }
            return property;
        }
    }
}
