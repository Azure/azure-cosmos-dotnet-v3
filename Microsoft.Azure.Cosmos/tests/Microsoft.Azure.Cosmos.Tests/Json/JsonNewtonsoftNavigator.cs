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

    internal sealed class JsonNewtonsoftNavigator : JsonNavigator
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

        public override JsonSerializationFormat SerializationFormat => JsonSerializationFormat.Text;

        public override IJsonNavigatorNode GetArrayItemAt(IJsonNavigatorNode arrayNode, int index)
        {
            JArray jArray = ((NewtonsoftNode)arrayNode).JToken as JArray;
            return new NewtonsoftNode(jArray[index], this.JTokenToJsonNodeType(jArray[index]));
        }

        public override bool TryGetBufferedBinaryValue(IJsonNavigatorNode binaryNode, out ReadOnlyMemory<byte> bufferedBinaryValue)
        {
            throw new NotImplementedException();
        }

        public override int GetArrayItemCount(IJsonNavigatorNode arrayNode)
        {
            JArray jArray = ((NewtonsoftNode)arrayNode).JToken as JArray;
            return jArray.Count;
        }

        public override IEnumerable<IJsonNavigatorNode> GetArrayItems(IJsonNavigatorNode arrayNode)
        {
            JArray jArray = ((NewtonsoftNode)arrayNode).JToken as JArray;
            foreach (JToken arrayItem in jArray)
            {
                yield return new NewtonsoftNode(arrayItem, this.JTokenToJsonNodeType(arrayItem));
            }
        }

        private JsonNodeType JTokenToJsonNodeType(JToken jToken)
        {
            return jToken.Type switch
            {
                JTokenType.Object => JsonNodeType.Object,
                JTokenType.Array => JsonNodeType.Array,
                JTokenType.Integer or JTokenType.Float => JsonNodeType.Number64,
                JTokenType.String => JsonNodeType.String,
                JTokenType.Boolean => ((bool)jToken) ? JsonNodeType.True : JsonNodeType.False,
                JTokenType.Null or JTokenType.Undefined => JsonNodeType.Null,
                JTokenType.Constructor or JTokenType.Property or JTokenType.Comment or JTokenType.Date or JTokenType.Raw or JTokenType.Bytes or JTokenType.Guid or JTokenType.Uri or JTokenType.TimeSpan => JsonNodeType.String,
                _ => throw new InvalidOperationException(),
            };
        }

        public override JsonNodeType GetNodeType(IJsonNavigatorNode node)
        {
            return ((NewtonsoftNode)node).JsonNodeType;
        }

        public override Number64 GetNumber64Value(IJsonNavigatorNode numberNode)
        {
            return (double)((NewtonsoftNode)numberNode).JToken;
        }

        public override IEnumerable<ObjectProperty> GetObjectProperties(IJsonNavigatorNode objectNode)
        {
            JObject jObject = ((NewtonsoftNode)objectNode).JToken as JObject;
            foreach (KeyValuePair<string, JToken> kvp in jObject)
            {
                yield return new ObjectProperty(
                    new NewtonsoftNode(JToken.FromObject(kvp.Key), JsonNodeType.FieldName),
                    new NewtonsoftNode(kvp.Value, this.JTokenToJsonNodeType(kvp.Value)));
            }
        }

        public override int GetObjectPropertyCount(IJsonNavigatorNode objectNode)
        {
            JObject jObject = ((NewtonsoftNode)objectNode).JToken as JObject;
            return jObject.Count;
        }

        public override IJsonNavigatorNode GetRootNode()
        {
            return this.root;
        }

        public override UtfAnyString GetStringValue(IJsonNavigatorNode stringNode)
        {
            return (string)((NewtonsoftNode)stringNode).JToken;
        }

        public override sbyte GetInt8Value(IJsonNavigatorNode numberNode)
        {
            throw new NotImplementedException();
        }

        public override short GetInt16Value(IJsonNavigatorNode numberNode)
        {
            throw new NotImplementedException();
        }

        public override int GetInt32Value(IJsonNavigatorNode numberNode)
        {
            throw new NotImplementedException();
        }

        public override long GetInt64Value(IJsonNavigatorNode numberNode)
        {
            throw new NotImplementedException();
        }

        public override float GetFloat32Value(IJsonNavigatorNode numberNode)
        {
            throw new NotImplementedException();
        }

        public override double GetFloat64Value(IJsonNavigatorNode numberNode)
        {
            throw new NotImplementedException();
        }

        public override uint GetUInt32Value(IJsonNavigatorNode numberNode)
        {
            throw new NotImplementedException();
        }

        public override Guid GetGuidValue(IJsonNavigatorNode guidNode)
        {
            throw new NotImplementedException();
        }

        public override ReadOnlyMemory<byte> GetBinaryValue(IJsonNavigatorNode binaryNode)
        {
            throw new NotImplementedException();
        }

        public override bool TryGetBufferedStringValue(IJsonNavigatorNode stringNode, out Utf8Memory bufferedStringValue)
        {
            bufferedStringValue = default;
            return false;
        }

        public override bool TryGetObjectProperty(IJsonNavigatorNode objectNode, string propertyName, out ObjectProperty objectProperty)
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

        public bool TryGetBufferedStringValue(IJsonNavigatorNode stringNode, out Utf8Span bufferedUtf8StringValue)
        {
            bufferedUtf8StringValue = default;
            return false;
        }

        public override IJsonReader CreateReader(IJsonNavigatorNode jsonNavigatorNode)
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