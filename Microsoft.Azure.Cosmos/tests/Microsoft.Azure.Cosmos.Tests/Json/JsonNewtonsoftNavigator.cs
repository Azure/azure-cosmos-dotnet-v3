//-----------------------------------------------------------------------
// <copyright file="JsonNewtonsoftNavigator.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Microsoft.Azure.Cosmos.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class JsonNewtonsoftNavigator : IJsonNavigator
    {
        private readonly NewtonsoftNode root;

        public JsonNewtonsoftNavigator(string input)
        {
            Newtonsoft.Json.JsonReader reader = new Newtonsoft.Json.JsonTextReader(new StringReader(input))
            {
                DateParseHandling = Newtonsoft.Json.DateParseHandling.None
            };

            JToken rootJToken = JToken.Load(reader);

            this.root = new NewtonsoftNode(rootJToken, this.JTokenToJsonNodeType(rootJToken));
        }

        public JsonSerializationFormat SerializationFormat => JsonSerializationFormat.Text;

        public IJsonNavigatorNode GetArrayItemAt(IJsonNavigatorNode arrayNode, int index)
        {
            JArray jArray = ((NewtonsoftNode)arrayNode).JToken as JArray;
            return new NewtonsoftNode(jArray[index], this.JTokenToJsonNodeType(jArray[index]));
        }

        public bool TryGetBufferedBinaryValue(IJsonNavigatorNode binaryNode, out ReadOnlyMemory<byte> bufferedBinaryValue)
        {
            throw new NotImplementedException();
        }

        public int GetArrayItemCount(IJsonNavigatorNode arrayNode)
        {
            JArray jArray = ((NewtonsoftNode)arrayNode).JToken as JArray;
            return jArray.Count;
        }

        public IEnumerable<IJsonNavigatorNode> GetArrayItems(IJsonNavigatorNode arrayNode)
        {
            JArray jArray = ((NewtonsoftNode)arrayNode).JToken as JArray;
            foreach (JToken arrayItem in jArray)
            {
                yield return new NewtonsoftNode(arrayItem, this.JTokenToJsonNodeType(arrayItem));
            }
        }

        private JsonNodeType JTokenToJsonNodeType(JToken jToken)
        {
            switch (jToken.Type)
            {
                case JTokenType.Object:
                    return JsonNodeType.Object;
                case JTokenType.Array:
                    return JsonNodeType.Array;
                case JTokenType.Integer:
                case JTokenType.Float:
                    return JsonNodeType.Number64;
                case JTokenType.String:
                    return JsonNodeType.String;
                case JTokenType.Boolean:
                    return ((bool)jToken) ? JsonNodeType.True : JsonNodeType.False;
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return JsonNodeType.Null;
                case JTokenType.Constructor:
                case JTokenType.Property:
                case JTokenType.Comment:
                case JTokenType.Date:
                case JTokenType.Raw:
                case JTokenType.Bytes:
                case JTokenType.Guid:
                case JTokenType.Uri:
                case JTokenType.TimeSpan:
                    return JsonNodeType.String;
                default:
                    throw new InvalidOperationException();
            }
        }

        public JsonNodeType GetNodeType(IJsonNavigatorNode node)
        {
            return ((NewtonsoftNode)node).JsonNodeType;
        }

        public Number64 GetNumber64Value(IJsonNavigatorNode numberNode)
        {
            return (double)((NewtonsoftNode)numberNode).JToken;
        }

        public IEnumerable<ObjectProperty> GetObjectProperties(IJsonNavigatorNode objectNode)
        {
            JObject jObject = ((NewtonsoftNode)objectNode).JToken as JObject;
            foreach (KeyValuePair<string, JToken> kvp in jObject)
            {
                yield return new ObjectProperty(
                    new NewtonsoftNode(JToken.FromObject(kvp.Key), JsonNodeType.FieldName),
                    new NewtonsoftNode(kvp.Value, this.JTokenToJsonNodeType(kvp.Value)));
            }
        }

        public int GetObjectPropertyCount(IJsonNavigatorNode objectNode)
        {
            JObject jObject = ((NewtonsoftNode)objectNode).JToken as JObject;
            return jObject.Count;
        }

        public IJsonNavigatorNode GetRootNode()
        {
            return this.root;
        }

        public string GetStringValue(IJsonNavigatorNode stringNode)
        {
            return (string)((NewtonsoftNode)stringNode).JToken;
        }

        public sbyte GetInt8Value(IJsonNavigatorNode numberNode)
        {
            throw new NotImplementedException();
        }

        public short GetInt16Value(IJsonNavigatorNode numberNode)
        {
            throw new NotImplementedException();
        }

        public int GetInt32Value(IJsonNavigatorNode numberNode)
        {
            throw new NotImplementedException();
        }

        public long GetInt64Value(IJsonNavigatorNode numberNode)
        {
            throw new NotImplementedException();
        }

        public float GetFloat32Value(IJsonNavigatorNode numberNode)
        {
            throw new NotImplementedException();
        }

        public double GetFloat64Value(IJsonNavigatorNode numberNode)
        {
            throw new NotImplementedException();
        }

        public uint GetUInt32Value(IJsonNavigatorNode numberNode)
        {
            throw new NotImplementedException();
        }

        public Guid GetGuidValue(IJsonNavigatorNode guidNode)
        {
            throw new NotImplementedException();
        }

        public ReadOnlyMemory<byte> GetBinaryValue(IJsonNavigatorNode binaryNode)
        {
            throw new NotImplementedException();
        }

        public bool TryGetBufferedStringValue(IJsonNavigatorNode stringNode, out Utf8Memory bufferedStringValue)
        {
            bufferedStringValue = default;
            return false;
        }

        public bool TryGetObjectProperty(IJsonNavigatorNode objectNode, string propertyName, out ObjectProperty objectProperty)
        {
            objectProperty = default;
            JObject jObject = ((NewtonsoftNode)objectNode).JToken as JObject;
            if (jObject.TryGetValue(propertyName, out JToken jToken))
            {
                objectProperty = new ObjectProperty(
                    new NewtonsoftNode(JToken.FromObject(propertyName), JsonNodeType.FieldName),
                    new NewtonsoftNode(jToken, this.JTokenToJsonNodeType(jToken)));
                return true;
            }

            return false;
        }

        public bool TryGetBufferedRawJson(IJsonNavigatorNode jsonNode, out ReadOnlyMemory<byte> bufferedRawJson)
        {
            bufferedRawJson = null;
            return false;
        }

        public bool TryGetBufferedStringValue(IJsonNavigatorNode stringNode, out Utf8Span bufferedUtf8StringValue)
        {
            bufferedUtf8StringValue = default;
            return false;
        }

        public T Materialize<T>(Newtonsoft.Json.JsonSerializer jsonSerializer, IJsonNavigatorNode jsonNavigatorNode)
        {
            throw new NotImplementedException();
        }

        private readonly struct NewtonsoftNode : IJsonNavigatorNode
        {
            public NewtonsoftNode(JToken jToken, JsonNodeType jsonNodeType)
            {
                this.JToken = jToken;
                this.JsonNodeType = jsonNodeType;
            }

            public JToken JToken { get; }
            public JsonNodeType JsonNodeType { get; }
        }
    }
}
