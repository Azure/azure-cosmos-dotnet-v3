namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json.NewtonsoftInterop;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
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
            //// TODO use the newtonsoft wrapper for this.
            return CosmosElement.Create(Encoding.UTF8.GetBytes(JToken.Load(reader).ToString()));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JsonNewtonsoftInteropWriter writerInterop = new JsonNewtonsoftInteropWriter(writer);
            CosmosElement cosmosElement = value as CosmosElement;
            cosmosElement.WriteTo(writerInterop);
        }

        //private static CosmosElement ReadCosmosElementFromReader(JsonReader reader)
        //{
        //    CosmosElement cosmosElement;
        //    if (reader.TokenType == JsonToken.Integer)
        //    {
        //        cosmosElement = CosmosNumber.Create((long)reader.Value);
        //    }
        //    else if (reader.TokenType == JsonToken.Float)
        //    {
        //        cosmosElement = CosmosNumber.Create((double)reader.Value);
        //    }
        //    else if (reader.TokenType == JsonToken.String || reader.TokenType == JsonToken.Date)
        //    {
        //        cosmosElement = CosmosString.Create(reader.Value.ToString());
        //    }
        //    else if (reader.TokenType == JsonToken.Null)
        //    {
        //        cosmosElement = CosmosNull.Create();
        //    }
        //    else if (reader.TokenType == JsonToken.Boolean)
        //    {
        //        cosmosElement = CosmosBoolean.Create((bool)reader.Value);
        //    }
        //    else if (reader.TokenType == JsonToken.StartArray)
        //    {
        //        List<CosmosElement> cosmosElements = new List<CosmosElement>();
        //        // Skip Array Start Token
        //        reader.Read();
        //        while(reader.TokenType != JsonToken.EndArray)
        //        {
        //            CosmosElement arrayItem = ReadCosmosElementFromReader(reader);
        //            cosmosElements.Add(arrayItem);
        //        }

        //        cosmosElement = CosmosArray.Create(cosmosElements);
        //    }
        //    else if (reader.TokenType == JsonToken.StartObject)
        //    {
        //        Dictionary<string, CosmosElement> dictionary = new Dictionary<string, CosmosElement>();
        //        // Skip Object Start Token
        //        reader.Read();
        //        while (reader.TokenType != JsonToken.EndObject)
        //        {
        //            string key = (string)reader.Value;
        //            reader.Read();

        //            CosmosElement value = ReadCosmosElementFromReader(reader);

        //            dictionary[key] = value;
        //        }

        //        cosmosElement = CosmosObject.Create(dictionary);
        //    }
        //    else
        //    {
        //        throw new ArgumentException($"Unknown type: {reader.ValueType}");
        //    }

        //    reader.Read();

        //    return cosmosElement;
        //}
    }
}
