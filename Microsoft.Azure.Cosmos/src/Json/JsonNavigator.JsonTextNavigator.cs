//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Partial class that wraps the private JsonTextNavigator
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    abstract partial class JsonNavigator : IJsonNavigator
    {
        /// <summary>
        /// JsonNavigator that know how to navigate JSONs in text serialization.
        /// Internally the navigator uses a <see cref="JsonTextParser"/> to from an AST of the JSON and the rest of the methods are just letting you traverse the materialized tree.
        /// </summary>
        private sealed class JsonTextNavigator : JsonNavigator
        {
            private readonly JsonTextNode rootNode;

            /// <summary>
            /// Initializes a new instance of the <see cref="JsonTextNavigator"/> class.
            /// </summary>
            /// <param name="buffer">The (UTF-8) buffer to navigate.</param>
            /// <param name="skipValidation">whether to skip validation or not.</param>
            public JsonTextNavigator(byte[] buffer, bool skipValidation = false)
            {
                IJsonReader jsonTextReader = JsonReader.Create(buffer: buffer, jsonStringDictionary: null, skipValidation: skipValidation);
                if (jsonTextReader.SerializationFormat != JsonSerializationFormat.Text)
                {
                    throw new ArgumentException("jsonTextReader's serialization format must actually be text");
                }

                this.rootNode = JsonTextParser.Parse(jsonTextReader);
            }

            /// <summary>
            /// Gets the <see cref="JsonSerializationFormat"/> for the IJsonNavigator.
            /// </summary>
            public override JsonSerializationFormat SerializationFormat
            {
                get
                {
                    return JsonSerializationFormat.Text;
                }
            }

            /// <summary>
            /// Gets <see cref="IJsonNavigatorNode"/> of the root node.
            /// </summary>
            /// <returns><see cref="IJsonNavigatorNode"/> corresponding to the root node.</returns>
            public override IJsonNavigatorNode GetRootNode()
            {
                return this.rootNode;
            }

            /// <summary>
            /// Gets the <see cref="JsonNodeType"/> type for a particular node
            /// </summary>
            /// <param name="node">The <see cref="IJsonNavigatorNode"/> of the node you want to know the type of</param>
            /// <returns><see cref="JsonNodeType"/> for the node</returns>
            public override JsonNodeType GetNodeType(IJsonNavigatorNode node)
            {
                if (node == null)
                {
                    throw new ArgumentNullException("node");
                }

                JsonTextNode jsonTextNode = node as JsonTextNode;
                if (jsonTextNode == null)
                {
                    throw new ArgumentException("node must actually be a text node.");
                }

                return jsonTextNode.JsonNodeType;
            }

            /// <summary>
            /// Gets the numeric value for a node
            /// </summary>
            /// <param name="numberNavigatorNode">The <see cref="IJsonNavigatorNode"/> of the node you want the number value from.</param>
            /// <returns>A double that represents the number value in the node.</returns>
            public override double GetNumberValue(IJsonNavigatorNode numberNavigatorNode)
            {
                if (numberNavigatorNode == null)
                {
                    throw new ArgumentNullException("numberNavigatorNode");
                }

                NumberNode numberNode = numberNavigatorNode as NumberNode;
                if (numberNode == null)
                {
                    throw new ArgumentException("numberNavigatorNode must actually be a number node.");
                }

                return numberNode.Value;
            }

            /// <summary>
            /// Tries to get the buffered string value from a node.
            /// </summary>
            /// <param name="stringNode">The <see cref="IJsonNavigatorNode"/> of the node to get the buffered string from.</param>
            /// <param name="bufferedStringValue">The buffered string value if possible</param>
            /// <returns><code>true</code> if the JsonNavigator successfully got the buffered string value; <code>false</code> if the JsonNavigator failed to get the buffered string value.</returns>
            public override bool TryGetBufferedStringValue(IJsonNavigatorNode stringNode, out IReadOnlyList<byte> bufferedStringValue)
            {
                bufferedStringValue = null;
                return false;
            }

            /// <summary>
            /// Gets a string value from a node.
            /// </summary>
            /// <param name="stringNode">The <see cref="IJsonNavigatorNode"/> of the node to get the string value from.</param>
            /// <returns>The string value from the node.</returns>
            public override string GetStringValue(IJsonNavigatorNode stringNode)
            {
                if (stringNode == null)
                {
                    throw new ArgumentNullException("stringNode");
                }

                StringNodeBase stringValueNode = stringNode as StringNodeBase;
                if (stringValueNode == null)
                {
                    throw new ArgumentException("stringNode must actually be a number node.");
                }

                return stringValueNode.Value;
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

            public override IReadOnlyList<byte> GetBinaryValue(IJsonNavigatorNode binaryNode)
            {
                throw new NotImplementedException();
            }

            public override bool TryGetBufferedBinaryValue(IJsonNavigatorNode binaryNode, out IReadOnlyList<byte> bufferedBinaryValue)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Gets the number of elements in an array node.
            /// </summary>
            /// <param name="arrayNavigatorNode">The <see cref="IJsonNavigatorNode"/> of the (array) node to get the count of.</param>
            /// <returns>The number of elements in the array node.</returns>
            public override int GetArrayItemCount(IJsonNavigatorNode arrayNavigatorNode)
            {
                if (arrayNavigatorNode == null)
                {
                    throw new ArgumentNullException("arrayNavigatorNode");
                }

                ArrayNode arrayNode = arrayNavigatorNode as ArrayNode;
                if (arrayNode == null)
                {
                    throw new ArgumentException("arrayNavigatorNode must actually be an array node");
                }

                return arrayNode.Items.Count;
            }

            /// <summary>
            /// Gets the node at a particular index of an array node
            /// </summary>
            /// <param name="arrayNavigatorNode">The <see cref="IJsonNavigatorNode"/> of the (array) node to index from.</param>
            /// <param name="index">The offset into the array</param>
            /// <returns>The <see cref="IJsonNavigatorNode"/> of the node at a particular index of an array node</returns>
            public override IJsonNavigatorNode GetArrayItemAt(IJsonNavigatorNode arrayNavigatorNode, int index)
            {
                if (arrayNavigatorNode == null)
                {
                    throw new ArgumentNullException("arrayNode");
                }

                ArrayNode arrayNode = arrayNavigatorNode as ArrayNode;
                if (arrayNode == null)
                {
                    throw new ArgumentException("arrayNavigatorNode must actually be an array node");
                }

                return arrayNode.Items[index];
            }

            /// <summary>
            /// Gets an IEnumerable of <see cref="IJsonNavigatorNode"/>s for an arrayNode.
            /// </summary>
            /// <param name="arrayNavigatorNode">The <see cref="IJsonNavigatorNode"/> of the array to get the items from</param>
            /// <returns>The IEnumerable of <see cref="IJsonNavigatorNode"/>s for an arrayNode.</returns>
            public override IEnumerable<IJsonNavigatorNode> GetArrayItems(IJsonNavigatorNode arrayNavigatorNode)
            {
                if (arrayNavigatorNode == null)
                {
                    throw new ArgumentNullException("arrayNode");
                }

                ArrayNode arrayNode = arrayNavigatorNode as ArrayNode;
                if (arrayNode == null)
                {
                    throw new ArgumentException("arrayNavigatorNode must actually be an array node");
                }

                return arrayNode.Items;
            }

            /// <summary>
            /// Gets the number of properties in an object node.
            /// </summary>
            /// <param name="objectNavigatorNode">The <see cref="IJsonNavigatorNode"/> of node to get the property count from.</param>
            /// <returns>The number of properties in an object node.</returns>
            public override int GetObjectPropertyCount(IJsonNavigatorNode objectNavigatorNode)
            {
                if (objectNavigatorNode == null)
                {
                    throw new ArgumentNullException("objectNode");
                }

                ObjectNode objectNode = objectNavigatorNode as ObjectNode;
                if (objectNode == null)
                {
                    throw new ArgumentException("objectNavigatorNode must actually be an array node");
                }

                return objectNode.Properties.Count;
            }

            /// <summary>
            /// Tries to get a object property from an object with a particular property name.
            /// </summary>
            /// <param name="objectNavigatorNode">The <see cref="ObjectProperty"/> of object node to get a property from.</param>
            /// <param name="propertyName">The name of the property to search for.</param>
            /// <param name="objectProperty">The <see cref="IJsonNavigatorNode"/> with the specified property name if it exists.</param>
            /// <returns><code>true</code> if the JsonNavigator successfully found the <see cref="ObjectProperty"/> with the specified property name; <code>false</code> otherwise.</returns>
            public override bool TryGetObjectProperty(IJsonNavigatorNode objectNavigatorNode, string propertyName, out ObjectProperty objectProperty)
            {
                if (objectNavigatorNode == null)
                {
                    throw new ArgumentNullException("objectNavigatorNode");
                }

                if (propertyName == null)
                {
                    throw new ArgumentNullException("propertyName");
                }

                ObjectNode objectNode = objectNavigatorNode as ObjectNode;
                if (objectNode == null)
                {
                    throw new ArgumentException("objectNavigatorNode must actually be an array node");
                }

                objectProperty = default(ObjectProperty);
                IReadOnlyList<ObjectProperty> properties = ((ObjectNode)objectNode).Properties;
                foreach (ObjectProperty property in properties)
                {
                    if (this.GetStringValue(property.NameNode) == propertyName)
                    {
                        objectProperty = property;
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Gets an IEnumerable of <see cref="IJsonNavigatorNode"/> properties from an object node.
            /// </summary>
            /// <param name="objectNavigatorNode">The <see cref="IJsonNavigatorNode"/> of object node to get the properties from.</param>
            /// <returns>The IEnumerable of <see cref="IJsonNavigatorNode"/> properties from an object node.</returns>
            public override IEnumerable<ObjectProperty> GetObjectProperties(IJsonNavigatorNode objectNavigatorNode)
            {
                if (objectNavigatorNode == null)
                {
                    throw new ArgumentNullException("objectNode");
                }

                ObjectNode objectNode = objectNavigatorNode as ObjectNode;
                if (objectNode == null)
                {
                    throw new ArgumentException("objectNavigatorNode must actually be an array node");
                }

                return objectNode.Properties;
            }

            /// <summary>
            /// Tries to get the buffered raw json
            /// </summary>
            /// <param name="jsonNode">The json node of interest</param>
            /// <param name="bufferedRawJson">The raw json.</param>
            /// <returns>True if bufferedRawJson was set. False otherwise.</returns>
            public override bool TryGetBufferedRawJson(IJsonNavigatorNode jsonNode, out IReadOnlyList<byte> bufferedRawJson)
            {
                throw new NotImplementedException();
            }

            #region JsonTextParser
            /// <summary>
            /// The JsonTextParser class is used to get a JSON AST / DOM from plaintext using a JsonTextReader as a lexer / tokenizer.
            /// Internally the parser is implemented as an LL(1) parser, since JSON is unambiguous and we can just parse it using recursive decent.
            /// </summary>
            private static class JsonTextParser
            {
                /// <summary>
                /// Gets the root node of a JSON AST from a jsonTextReader.
                /// </summary>
                /// <param name="jsonTextReader">The reader to use as a lexer / tokenizer</param>
                /// <returns>The root node of a JSON AST from a jsonTextReader.</returns>
                public static JsonTextNode Parse(IJsonReader jsonTextReader)
                {
                    if (jsonTextReader.SerializationFormat != JsonSerializationFormat.Text)
                    {
                        throw new ArgumentException("jsonTextReader's serialization format must actually be text");
                    }

                    // Read past the json object not started state.
                    jsonTextReader.Read();

                    JsonTextNode rootNode = JsonTextParser.ParseNode(jsonTextReader);

                    // Make sure that we are at the end of the file.
                    if (jsonTextReader.Read())
                    {
                        throw new ArgumentException("Did not fully parse json");
                    }

                    return rootNode;
                }

                /// <summary>
                /// Parses out a JSON array AST node with a jsonTextReader.
                /// </summary>
                /// <param name="jsonTextReader">The reader to use as a lexer / tokenizer</param>
                /// <returns>JSON array AST node</returns>
                private static ArrayNode ParseArrayNode(IJsonReader jsonTextReader)
                {
                    List<JsonTextNode> items = new List<JsonTextNode>();

                    // consume the begin array token
                    jsonTextReader.Read();

                    while (jsonTextReader.CurrentTokenType != JsonTokenType.EndArray)
                    {
                        items.Add(JsonTextParser.ParseNode(jsonTextReader));
                    }

                    // consume the end array token
                    jsonTextReader.Read();

                    return ArrayNode.Create(items);
                }

                /// <summary>
                /// Parses out a JSON object AST node with a jsonTextReader.
                /// </summary>
                /// <param name="jsonTextReader">The reader to use as a lexer / tokenizer</param>
                /// <returns>JSON object AST node</returns>
                private static ObjectNode ParseObjectNode(IJsonReader jsonTextReader)
                {
                    List<ObjectProperty> properties = new List<ObjectProperty>();

                    // consume the begin object token
                    jsonTextReader.Read();

                    while (jsonTextReader.CurrentTokenType != JsonTokenType.EndObject)
                    {
                        ObjectProperty property = JsonTextParser.ParsePropertyNode(jsonTextReader);
                        properties.Add(property);
                    }

                    // consume the end object token
                    jsonTextReader.Read();

                    return ObjectNode.Create(properties);
                }

                /// <summary>
                /// Parses out a JSON string AST node with a jsonTextReader.
                /// </summary>
                /// <param name="jsonTextReader">The reader to use as a lexer / tokenizer</param>
                /// <returns>JSON string AST node</returns>
                private static StringNode ParseStringNode(IJsonReader jsonTextReader)
                {
                    StringNode stringNode = StringNode.Create((ArraySegment<byte>)jsonTextReader.GetBufferedRawJsonToken());

                    // consume the string from the reader
                    jsonTextReader.Read();

                    return stringNode;
                }

                /// <summary>
                /// Parses out a JSON number AST node with a jsonTextReader.
                /// </summary>
                /// <param name="jsonTextReader">The reader to use as a lexer / tokenizer</param>
                /// <returns>JSON number AST node</returns>
                private static NumberNode ParseNumberNode(IJsonReader jsonTextReader)
                {
                    NumberNode numberNode = NumberNode.Create((ArraySegment<byte>)jsonTextReader.GetBufferedRawJsonToken());

                    // consume the number from the reader
                    jsonTextReader.Read();

                    return numberNode;
                }

                /// <summary>
                /// Parses out a JSON true AST node with a jsonTextReader.
                /// </summary>
                /// <param name="jsonTextReader">The reader to use as a lexer / tokenizer</param>
                /// <returns>JSON true AST node</returns>
                private static TrueNode ParseTrueNode(IJsonReader jsonTextReader)
                {
                    // consume the true token from the reader
                    jsonTextReader.Read();

                    return TrueNode.Create();
                }

                /// <summary>
                /// Parses out a JSON false AST node with a jsonTextReader.
                /// </summary>
                /// <param name="jsonTextReader">The reader to use as a lexer / tokenizer</param>
                /// <returns>JSON true AST node</returns>
                private static FalseNode ParseFalseNode(IJsonReader jsonTextReader)
                {
                    // consume the false token from the reader
                    jsonTextReader.Read();

                    return FalseNode.Create();
                }

                /// <summary>
                /// Parses out a JSON null AST node with a jsonTextReader.
                /// </summary>
                /// <param name="jsonTextReader">The reader to use as a lexer / tokenizer</param>
                /// <returns>JSON null AST node</returns>
                private static NullNode ParseNullNode(IJsonReader jsonTextReader)
                {
                    // consume the null token from the reader
                    jsonTextReader.Read();

                    return NullNode.Create();
                }

                /// <summary>
                /// Parses out a JSON property AST node with a jsonTextReader.
                /// </summary>
                /// <param name="jsonTextReader">The reader to use as a lexer / tokenizer</param>
                /// <returns>JSON property AST node</returns>
                private static ObjectProperty ParsePropertyNode(IJsonReader jsonTextReader)
                {
                    FieldNameNode fieldName = FieldNameNode.Create((ArraySegment<byte>)jsonTextReader.GetBufferedRawJsonToken());

                    // Consume the fieldname from the jsonreader
                    jsonTextReader.Read();

                    JsonTextNode value = JsonTextParser.ParseNode(jsonTextReader);
                    return new ObjectProperty(fieldName, value);
                }

                /// <summary>
                /// Parses out a JSON AST node with a jsonTextReader.
                /// </summary>
                /// <param name="jsonTextReader">The reader to use as a lexer / tokenizer</param>
                /// <returns>JSON AST node (type determined by the reader)</returns>
                private static JsonTextNode ParseNode(IJsonReader jsonTextReader)
                {
                    JsonTextNode node;
                    switch (jsonTextReader.CurrentTokenType)
                    {
                        case JsonTokenType.BeginArray:
                            node = JsonTextParser.ParseArrayNode(jsonTextReader);
                            break;
                        case JsonTokenType.BeginObject:
                            node = JsonTextParser.ParseObjectNode(jsonTextReader);
                            break;
                        case JsonTokenType.String:
                            node = JsonTextParser.ParseStringNode(jsonTextReader);
                            break;
                        case JsonTokenType.Number:
                            node = JsonTextParser.ParseNumberNode(jsonTextReader);
                            break;
                        case JsonTokenType.True:
                            node = JsonTextParser.ParseTrueNode(jsonTextReader);
                            break;
                        case JsonTokenType.False:
                            node = JsonTextParser.ParseFalseNode(jsonTextReader);
                            break;
                        case JsonTokenType.Null:
                            node = JsonTextParser.ParseNullNode(jsonTextReader);
                            break;
                        default:
                            throw new JsonInvalidTokenException();
                    }

                    return node;
                }
            }
            #endregion

            #region Nodes
            private sealed class ArrayNode : JsonTextNode
            {
                private static readonly ArrayNode Empty = new ArrayNode(new List<JsonTextNode>());
                private readonly List<JsonTextNode> items;

                private ArrayNode(List<JsonTextNode> items)
                    : base(JsonNodeType.Array)
                {
                    this.items = items;
                }

                public IReadOnlyList<JsonTextNode> Items
                {
                    get
                    {
                        return this.items;
                    }
                }

                public static ArrayNode Create(List<JsonTextNode> items)
                {
                    if (items.Count == 0)
                    {
                        return ArrayNode.Empty;
                    }

                    return new ArrayNode(items);
                }
            }

            private sealed class FalseNode : JsonTextNode
            {
                private static readonly FalseNode Instance = new FalseNode();

                private FalseNode()
                    : base(JsonNodeType.False)
                {
                }

                public static FalseNode Create()
                {
                    return FalseNode.Instance;
                }
            }

            private sealed class FieldNameNode : StringNodeBase
            {
                private static readonly FieldNameNode Empty = new FieldNameNode(string.Empty);

                private FieldNameNode(ArraySegment<byte> bufferedToken)
                    : base(bufferedToken, false)
                {
                }

                private FieldNameNode(string value)
                    : base(value, true)
                {
                }

                public static FieldNameNode Create(ArraySegment<byte> bufferedToken)
                {
                    if (bufferedToken.Count == 0)
                    {
                        return FieldNameNode.Empty;
                    }

                    // In the future we can have a flyweight dictionary for system strings.
                    return new FieldNameNode(bufferedToken);
                }
            }

            private abstract class JsonTextNode : IJsonNavigatorNode
            {
                private readonly JsonNodeType jsonNodeType;

                protected JsonTextNode(JsonNodeType jsonNodeType)
                {
                    this.jsonNodeType = jsonNodeType;
                }

                public JsonNodeType JsonNodeType
                {
                    get
                    {
                        return this.jsonNodeType;
                    }
                }
            }

            private sealed class NullNode : JsonTextNode
            {
                private static readonly NullNode Instance = new NullNode();

                private NullNode()
                    : base(JsonNodeType.Null)
                {
                }

                public static NullNode Create()
                {
                    return NullNode.Instance;
                }
            }

            private sealed class NumberNode : JsonTextNode
            {
                private static readonly NumberNode[] LiteralNumberNodes = new NumberNode[]
                {
                    new NumberNode(0),  new NumberNode(1),  new NumberNode(2),  new NumberNode(3),
                    new NumberNode(4),  new NumberNode(5),  new NumberNode(6),  new NumberNode(7),
                    new NumberNode(8),  new NumberNode(9),  new NumberNode(10), new NumberNode(11),
                    new NumberNode(12), new NumberNode(13), new NumberNode(14), new NumberNode(15),
                    new NumberNode(16), new NumberNode(17), new NumberNode(18), new NumberNode(19),
                    new NumberNode(20), new NumberNode(21), new NumberNode(22), new NumberNode(23),
                    new NumberNode(24), new NumberNode(25), new NumberNode(26), new NumberNode(27),
                    new NumberNode(28), new NumberNode(29), new NumberNode(30), new NumberNode(31),
                };

                private readonly Lazy<double> value;

                private NumberNode(ArraySegment<byte> bufferedToken)
                    : base(JsonNodeType.Number)
                {
                    this.value = new Lazy<double>(() => JsonTextUtil.GetNumberValue(bufferedToken));
                }

                private NumberNode(double value)
                    : base(JsonNodeType.Number)
                {
                    this.value = new Lazy<double>(() => value);
                }

                public double Value
                {
                    get
                    {
                        return this.value.Value;
                    }
                }

                public static NumberNode Create(ArraySegment<byte> bufferedToken)
                {
                    IReadOnlyList<byte> payload = bufferedToken;
                    if (payload.Count == 1 && payload[0] >= '0' && payload[0] <= '9')
                    {
                        // Single digit number.
                        return NumberNode.LiteralNumberNodes[payload[0] - '0'];
                    }

                    if (payload.Count == 2 && payload[0] >= '0' && payload[0] <= '9' && payload[1] >= '0' && payload[1] <= '9')
                    {
                        // Two digit number.
                        int index = ((payload[0] - '0') * 10) + (payload[1] - '0');
                        if (index >= 0 && index < NumberNode.LiteralNumberNodes.Length)
                        {
                            return NumberNode.LiteralNumberNodes[index];
                        }
                    }

                    return new NumberNode(bufferedToken);
                }
            }

            private sealed class ObjectNode : JsonTextNode
            {
                private static readonly ObjectNode Empty = new ObjectNode(new List<ObjectProperty>());
                private readonly List<ObjectProperty> properties;

                private ObjectNode(List<ObjectProperty> properties)
                    : base(JsonNodeType.Object)
                {
                    this.properties = properties;
                }

                public IReadOnlyList<ObjectProperty> Properties
                {
                    get { return this.properties; }
                }

                public static ObjectNode Create(List<ObjectProperty> properties)
                {
                    if (properties.Count == 0)
                    {
                        return ObjectNode.Empty;
                    }

                    return new ObjectNode(properties);
                }
            }

            private sealed class StringNode : StringNodeBase
            {
                private static readonly StringNode Empty = new StringNode(string.Empty);

                private StringNode(ArraySegment<byte> bufferedToken)
                    : base(bufferedToken, true)
                {
                }

                private StringNode(string value)
                    : base(value, true)
                {
                }

                public static StringNode Create(ArraySegment<byte> bufferedToken)
                {
                    if (bufferedToken.Count == 0)
                    {
                        return StringNode.Empty;
                    }

                    // In the future we can have a flyweight dictionary for system strings.
                    return new StringNode(bufferedToken);
                }
            }

            private abstract class StringNodeBase : JsonTextNode
            {
                private readonly Lazy<string> value;

                protected StringNodeBase(ArraySegment<byte> bufferedToken, bool isStringNode)
                    : base(isStringNode ? JsonNodeType.String : JsonNodeType.FieldName)
                {
                    this.value = new Lazy<string>(() => JsonTextUtil.GetStringValue(bufferedToken));
                }

                protected StringNodeBase(string value, bool isStringNode)
                    : base(isStringNode ? JsonNodeType.String : JsonNodeType.FieldName)
                {
                    this.value = new Lazy<string>(() => value);
                }

                public string Value
                {
                    get { return this.value.Value; }
                }
            }

            private sealed class TrueNode : JsonTextNode
            {
                private static readonly TrueNode Instance = new TrueNode();

                private TrueNode()
                    : base(JsonNodeType.True)
                {
                }

                public static TrueNode Create()
                {
                    return TrueNode.Instance;
                }
            }
            #endregion
        }
    }
}
