//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
#if NETSTANDARD15 || NETSTANDARD16
    using System.Reflection;
#endif
    using System.Text;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class CosmosElementJsonConverter : JsonConverter
    {
        private static readonly HashSet<Type> NumberTypes = new HashSet<Type>()
        {
            typeof(double),
            typeof(float),
            typeof(long),
            typeof(int),
            typeof(short),
            typeof(byte),
            typeof(uint),
            typeof(CosmosNumber),
        };

        private static readonly HashSet<Type> StringTypes = new HashSet<Type>()
        {
            typeof(string),
            typeof(CosmosString),
        };

        private static readonly HashSet<Type> NullTypes = new HashSet<Type>()
        {
            typeof(object),
            typeof(CosmosNull),
        };

        private static readonly HashSet<Type> ArrayTypes = new HashSet<Type>()
        {
            typeof(object[]),
            typeof(CosmosArray),
        };

        private static readonly HashSet<Type> ObjectTypes = new HashSet<Type>()
        {
            typeof(Dictionary<string, object>),
            typeof(CosmosObject),
        };

        private static readonly HashSet<Type> BooleanTypes = new HashSet<Type>()
        {
            typeof(bool),
            typeof(CosmosBoolean),
        };

        private static readonly HashSet<Type> ConvertableTypes = new HashSet<Type>(NumberTypes
            .Concat(StringTypes)
            .Concat(NullTypes)
            .Concat(ArrayTypes)
            .Concat(ObjectTypes)
            .Concat(BooleanTypes));

        public override bool CanConvert(Type objectType)
        {
#if NETSTANDARD15 || NETSTANDARD16
            return ConvertableTypes.Contains(objectType) || ConvertableTypes.Contains(objectType.GetTypeInfo().BaseType) || objectType == typeof(CosmosElement);
#else
            return ConvertableTypes.Contains(objectType) || ConvertableTypes.Contains(objectType.BaseType) || objectType == typeof(CosmosElement);
#endif
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            string json = JsonConvert.SerializeObject(token);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            return CosmosElement.Create(buffer);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            NewtonsoftToCosmosDBWriter writerInterop = NewtonsoftToCosmosDBWriter.CreateFromWriter(writer);
            CosmosElement cosmosElement = value as CosmosElement;
            cosmosElement.WriteTo(writerInterop);
        }
    }
#if INTERNAL
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
