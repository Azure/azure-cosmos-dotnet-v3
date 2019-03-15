namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json.NewtonsoftInterop;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    internal sealed class CosmosElementJsonConverter : JsonConverter
    {
        private static readonly HashSet<Type> NumberTypes = new HashSet<Type>()
        {
            typeof(double),
            typeof(float),
            typeof(long),
            typeof(int),
            typeof(short),
            typeof(byte),
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
            return ConvertableTypes.Contains(objectType) || ConvertableTypes.Contains(objectType.BaseType) || objectType == typeof(CosmosElement);
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
            JsonNewtonsoftWriter writerInterop = JsonNewtonsoftWriter.Create(writer);
            CosmosElement cosmosElement = value as CosmosElement;
            cosmosElement.WriteTo(writerInterop);
        }
    }
}
