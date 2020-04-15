//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core;

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
        /// Internally the navigator uses a <see cref="Parser"/> to from an AST of the JSON and the rest of the methods are just letting you traverse the materialized tree.
        /// </summary>
        private sealed class JsonTextNavigator : JsonNavigator
        {
            private readonly JsonTextNode rootNode;

            /// <summary>
            /// Initializes a new instance of the <see cref="JsonTextNavigator"/> class.
            /// </summary>
            /// <param name="buffer">The (UTF-8) buffer to navigate.</param>
            /// <param name="skipValidation">whether to skip validation or not.</param>
            public JsonTextNavigator(
                ReadOnlyMemory<byte> buffer,
                bool skipValidation = false)
            {
                IJsonReader jsonTextReader = JsonReader.Create(
                    buffer: buffer,
                    jsonStringDictionary: null,
                    skipValidation: skipValidation);
                if (jsonTextReader.SerializationFormat != JsonSerializationFormat.Text)
                {
                    throw new ArgumentException("jsonTextReader's serialization format must actually be text");
                }

                this.rootNode = Parser.Parse(jsonTextReader);
            }

            /// <inheritdoc />
            public override JsonSerializationFormat SerializationFormat
            {
                get
                {
                    return JsonSerializationFormat.Text;
                }
            }

            /// <inheritdoc />
            public override IJsonNavigatorNode GetRootNode()
            {
                return this.rootNode;
            }

            /// <inheritdoc />
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

            /// <inheritdoc />
            public override Number64 GetNumberValue(IJsonNavigatorNode numberNavigatorNode)
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

            /// <inheritdoc />
            public override bool TryGetBufferedStringValue(
                IJsonNavigatorNode navigatorNode,
                out Utf8Memory bufferedStringValue)
            {
                if (navigatorNode == null)
                {
                    throw new ArgumentNullException(nameof(navigatorNode));
                }

                if (!(navigatorNode is StringNodeBase stringNode))
                {
                    throw new ArgumentException($"{nameof(navigatorNode)} must actually be a number node.");
                }

                // For text we materialize the strings into UTF-16, so we can't get the buffered UTF-8 string.
                bufferedStringValue = default;
                return false;
            }

            /// <inheritdoc />
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

            /// <inheritdoc />
            public override sbyte GetInt8Value(IJsonNavigatorNode numberNode)
            {
                if (numberNode == null)
                {
                    throw new ArgumentNullException(nameof(numberNode));
                }

                if (!(numberNode is Int8Node int8Node))
                {
                    throw new ArgumentException($"{nameof(numberNode)} must actually be a {nameof(Int8Node)} node.");
                }

                return int8Node.Value;
            }

            /// <inheritdoc />
            public override short GetInt16Value(IJsonNavigatorNode numberNode)
            {
                if (numberNode == null)
                {
                    throw new ArgumentNullException(nameof(numberNode));
                }

                if (!(numberNode is Int16Node int16Node))
                {
                    throw new ArgumentException($"{nameof(numberNode)} must actually be a {nameof(Int16Node)} node.");
                }

                return int16Node.Value;
            }

            /// <inheritdoc />
            public override int GetInt32Value(IJsonNavigatorNode numberNode)
            {
                if (numberNode == null)
                {
                    throw new ArgumentNullException(nameof(numberNode));
                }

                if (!(numberNode is Int32Node int32Node))
                {
                    throw new ArgumentException($"{nameof(numberNode)} must actually be a {nameof(Int32Node)} node.");
                }

                return int32Node.Value;
            }

            /// <inheritdoc />
            public override long GetInt64Value(IJsonNavigatorNode numberNode)
            {
                if (numberNode == null)
                {
                    throw new ArgumentNullException(nameof(numberNode));
                }

                if (!(numberNode is Int64Node int64Node))
                {
                    throw new ArgumentException($"{nameof(numberNode)} must actually be a {nameof(Int64Node)} node.");
                }

                return int64Node.Value;
            }

            /// <inheritdoc />
            public override float GetFloat32Value(IJsonNavigatorNode numberNode)
            {
                if (numberNode == null)
                {
                    throw new ArgumentNullException(nameof(numberNode));
                }

                if (!(numberNode is Float32Node float32Node))
                {
                    throw new ArgumentException($"{nameof(numberNode)} must actually be a {nameof(float32Node)} node.");
                }

                return float32Node.Value;
            }

            /// <inheritdoc />
            public override double GetFloat64Value(IJsonNavigatorNode numberNode)
            {
                if (numberNode == null)
                {
                    throw new ArgumentNullException(nameof(numberNode));
                }

                if (!(numberNode is Float64Node float64Node))
                {
                    throw new ArgumentException($"{nameof(numberNode)} must actually be a {nameof(float64Node)} node.");
                }

                return float64Node.Value;
            }

            /// <inheritdoc />
            public override uint GetUInt32Value(IJsonNavigatorNode numberNode)
            {
                if (numberNode == null)
                {
                    throw new ArgumentNullException(nameof(numberNode));
                }

                if (!(numberNode is UInt32Node uInt32Node))
                {
                    throw new ArgumentException($"{nameof(numberNode)} must actually be a {nameof(uInt32Node)} node.");
                }

                return uInt32Node.Value;
            }

            /// <inheritdoc />
            public override Guid GetGuidValue(IJsonNavigatorNode node)
            {
                if (node == null)
                {
                    throw new ArgumentNullException(nameof(node));
                }

                if (!(node is GuidNode guidNode))
                {
                    throw new ArgumentException($"{nameof(node)} must actually be a {nameof(GuidNode)} node.");
                }

                return guidNode.Value;
            }

            /// <inheritdoc />
            public override ReadOnlyMemory<byte> GetBinaryValue(IJsonNavigatorNode node)
            {
                if (node == null)
                {
                    throw new ArgumentNullException(nameof(node));
                }

                if (!(node is BinaryNode binaryNode))
                {
                    throw new ArgumentException($"{nameof(node)} must actually be a {nameof(BinaryNode)} node.");
                }

                return binaryNode.Value;
            }

            /// <inheritdoc />
            public override bool TryGetBufferedBinaryValue(
                IJsonNavigatorNode binaryNode,
                out ReadOnlyMemory<byte> bufferedBinaryValue)
            {
                bufferedBinaryValue = default;
                return false;
            }

            /// <inheritdoc />
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

            /// <inheritdoc />
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

            /// <inheritdoc />
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

            /// <inheritdoc />
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

            /// <inheritdoc />
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

                if (!(objectNavigatorNode is ObjectNode objectNode))
                {
                    throw new ArgumentException("objectNavigatorNode must actually be an array node");
                }

                IReadOnlyList<ObjectProperty> properties = ((ObjectNode)objectNode).Properties;
                foreach (ObjectProperty property in properties)
                {
                    if (this.GetStringValue(property.NameNode) == propertyName)
                    {
                        objectProperty = property;
                        return true;
                    }
                }

                objectProperty = default;
                return false;
            }

            /// <inheritdoc />
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

            /// <inheritdoc />
            public override bool TryGetBufferedRawJson(
                IJsonNavigatorNode jsonNode,
                out ReadOnlyMemory<byte> bufferedRawJson)
            {
                bufferedRawJson = default;
                return false;
            }

            #region JsonTextParser
            /// <summary>
            /// The JsonTextParser class is used to get a JSON AST / DOM from plaintext using a JsonTextReader as a lexer / tokenizer.
            /// Internally the parser is implemented as an LL(1) parser, since JSON is unambiguous and we can just parse it using recursive decent.
            /// </summary>
            private static class Parser
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

                    JsonTextNode rootNode = Parser.ParseNode(jsonTextReader);

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
                        items.Add(Parser.ParseNode(jsonTextReader));
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
                        ObjectProperty property = Parser.ParsePropertyNode(jsonTextReader);
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
                    if (!jsonTextReader.TryGetBufferedRawJsonToken(out ReadOnlyMemory<byte> bufferedRawJsonToken))
                    {
                        throw new InvalidOperationException("Failed to get the buffered raw json token.");
                    }

                    StringNode stringNode = StringNode.Create(bufferedRawJsonToken);

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
                    if (!jsonTextReader.TryGetBufferedRawJsonToken(out ReadOnlyMemory<byte> bufferedRawJsonToken))
                    {
                        throw new InvalidOperationException("Failed to get the buffered raw json token.");
                    }

                    NumberNode numberNode = NumberNode.Create(bufferedRawJsonToken);

                    // consume the number from the reader
                    jsonTextReader.Read();

                    return numberNode;
                }

                private static IntegerNode ParseIntegerNode(IJsonReader jsonTextReader, JsonTokenType jsonTokenType)
                {
                    if (!jsonTextReader.TryGetBufferedRawJsonToken(out ReadOnlyMemory<byte> bufferedRawJsonToken))
                    {
                        throw new InvalidOperationException("Failed to get the buffered raw json token.");
                    }

                    IntegerNode integerNode;
                    switch (jsonTokenType)
                    {
                        case JsonTokenType.Int8:
                            integerNode = Int8Node.Create(bufferedRawJsonToken);
                            break;

                        case JsonTokenType.Int16:
                            integerNode = Int16Node.Create(bufferedRawJsonToken);
                            break;

                        case JsonTokenType.Int32:
                            integerNode = Int32Node.Create(bufferedRawJsonToken);
                            break;

                        case JsonTokenType.Int64:
                            integerNode = Int64Node.Create(bufferedRawJsonToken);
                            break;

                        case JsonTokenType.UInt32:
                            integerNode = UInt32Node.Create(bufferedRawJsonToken);
                            break;

                        default:
                            throw new ArgumentException($"Unknown {nameof(JsonTokenType)}: {jsonTokenType}");
                    }

                    // consume the integer from the reader
                    jsonTextReader.Read();

                    return integerNode;
                }

                private static FloatNode ParseFloatNode(IJsonReader jsonTextReader, JsonTokenType jsonTokenType)
                {
                    if (!jsonTextReader.TryGetBufferedRawJsonToken(out ReadOnlyMemory<byte> bufferedRawJsonToken))
                    {
                        throw new InvalidOperationException("Failed to get the buffered raw json token.");
                    }

                    FloatNode floatNode;
                    switch (jsonTokenType)
                    {
                        case JsonTokenType.Float32:
                            floatNode = Float32Node.Create(bufferedRawJsonToken);
                            break;

                        case JsonTokenType.Float64:
                            floatNode = Float64Node.Create(bufferedRawJsonToken);
                            break;
                        default:
                            throw new ArgumentException($"Unknown {nameof(JsonTokenType)}: {jsonTokenType}");
                    }

                    // consume the float from the reader
                    jsonTextReader.Read();

                    return floatNode;
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
                    if (!jsonTextReader.TryGetBufferedRawJsonToken(out ReadOnlyMemory<byte> bufferedRawJsonToken))
                    {
                        throw new InvalidOperationException("Failed to get the buffered raw json token.");
                    }

                    FieldNameNode fieldName = FieldNameNode.Create(bufferedRawJsonToken);

                    // Consume the fieldname from the jsonreader
                    jsonTextReader.Read();

                    JsonTextNode value = Parser.ParseNode(jsonTextReader);
                    return new ObjectProperty(fieldName, value);
                }

                private static GuidNode ParseGuidNode(IJsonReader jsonTextReader)
                {
                    if (!jsonTextReader.TryGetBufferedRawJsonToken(out ReadOnlyMemory<byte> bufferedRawJsonToken))
                    {
                        throw new InvalidOperationException("Failed to get the buffered raw json token.");
                    }

                    GuidNode node = GuidNode.Create(bufferedRawJsonToken);

                    // advance the reader forward.
                    jsonTextReader.Read();
                    return node;
                }

                private static BinaryNode ParseBinaryNode(IJsonReader jsonTextReader)
                {
                    if (!jsonTextReader.TryGetBufferedRawJsonToken(out ReadOnlyMemory<byte> bufferedRawJsonToken))
                    {
                        throw new InvalidOperationException("Failed to get the buffered raw json token.");
                    }

                    BinaryNode node = BinaryNode.Create(bufferedRawJsonToken);

                    // advance the reader forward.
                    jsonTextReader.Read();
                    return node;
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
                            node = Parser.ParseArrayNode(jsonTextReader);
                            break;

                        case JsonTokenType.BeginObject:
                            node = Parser.ParseObjectNode(jsonTextReader);
                            break;

                        case JsonTokenType.String:
                            node = Parser.ParseStringNode(jsonTextReader);
                            break;

                        case JsonTokenType.Number:
                            node = Parser.ParseNumberNode(jsonTextReader);
                            break;

                        case JsonTokenType.Float32:
                        case JsonTokenType.Float64:
                            node = Parser.ParseFloatNode(jsonTextReader, jsonTextReader.CurrentTokenType);
                            break;

                        case JsonTokenType.Int8:
                        case JsonTokenType.Int16:
                        case JsonTokenType.Int32:
                        case JsonTokenType.Int64:
                        case JsonTokenType.UInt32:
                            node = Parser.ParseIntegerNode(jsonTextReader, jsonTextReader.CurrentTokenType);
                            break;

                        case JsonTokenType.True:
                            node = Parser.ParseTrueNode(jsonTextReader);
                            break;
                        case JsonTokenType.False:
                            node = Parser.ParseFalseNode(jsonTextReader);
                            break;
                        case JsonTokenType.Null:
                            node = Parser.ParseNullNode(jsonTextReader);
                            break;
                        case JsonTokenType.Guid:
                            node = Parser.ParseGuidNode(jsonTextReader);
                            break;
                        case JsonTokenType.Binary:
                            node = Parser.ParseBinaryNode(jsonTextReader);
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

                private FieldNameNode(ReadOnlyMemory<byte> bufferedToken)
                    : base(bufferedToken, false)
                {
                }

                private FieldNameNode(string value)
                    : base(value, true)
                {
                }

                public static FieldNameNode Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    if (bufferedToken.Length == 0)
                    {
                        return FieldNameNode.Empty;
                    }

                    // In the future we can have a flyweight dictionary for system strings.
                    return new FieldNameNode(bufferedToken);
                }
            }

            private abstract class JsonTextNode : IJsonNavigatorNode
            {
                protected JsonTextNode(JsonNodeType jsonNodeType)
                {
                    this.JsonNodeType = jsonNodeType;
                }

                public JsonNodeType JsonNodeType
                {
                    get;
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

                private readonly Lazy<Number64> value;

                private NumberNode(ReadOnlyMemory<byte> bufferedToken)
                    : base(JsonNodeType.Number)
                {
                    this.value = new Lazy<Number64>(() => JsonTextParser.GetNumberValue(bufferedToken.Span));
                }

                private NumberNode(Number64 value)
                    : base(JsonNodeType.Number)
                {
                    this.value = new Lazy<Number64>(() => value);
                }

                public Number64 Value
                {
                    get
                    {
                        return this.value.Value;
                    }
                }

                public static NumberNode Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    ReadOnlySpan<byte> payload = bufferedToken.Span;
                    if (
                        (payload.Length == 1) &&
                        (payload[0] >= '0') &&
                        (payload[0] <= '9'))
                    {
                        // Single digit number.
                        return NumberNode.LiteralNumberNodes[payload[0] - '0'];
                    }

                    if (
                        (payload.Length == 2) &&
                        (payload[0] >= '0') &&
                        (payload[0] <= '9') &&
                        (payload[1] >= '0') &&
                        (payload[1] <= '9'))
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

                private StringNode(ReadOnlyMemory<byte> bufferedToken)
                    : base(bufferedToken, true)
                {
                }

                private StringNode(string value)
                    : base(value, true)
                {
                }

                public static StringNode Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    if (bufferedToken.Length == 0)
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

                protected StringNodeBase(
                    ReadOnlyMemory<byte> bufferedToken,
                    bool isStringNode)
                    : base(isStringNode ? JsonNodeType.String : JsonNodeType.FieldName)
                {
                    this.value = new Lazy<string>(() => JsonTextParser.GetStringValue(bufferedToken.Span));
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

            private abstract class IntegerNode : JsonTextNode
            {
                protected IntegerNode(JsonNodeType jsonNodeType)
                    : base(jsonNodeType)
                {
                }
            }

            private sealed class Int8Node : IntegerNode
            {
                private readonly Lazy<sbyte> lazyValue;

                private Int8Node(ReadOnlyMemory<byte> bufferedToken)
                    : base(JsonNodeType.Int8)
                {
                    this.lazyValue = new Lazy<sbyte>(() =>
                    {
                        sbyte value = JsonTextParser.GetInt8Value(bufferedToken.Span);
                        return value;
                    });
                }

                public sbyte Value
                {
                    get
                    {
                        return this.lazyValue.Value;
                    }
                }

                public static Int8Node Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    return new Int8Node(bufferedToken);
                }
            }

            private sealed class Int16Node : IntegerNode
            {
                private readonly Lazy<short> lazyValue;

                private Int16Node(ReadOnlyMemory<byte> bufferedToken)
                    : base(JsonNodeType.Int16)
                {
                    this.lazyValue = new Lazy<short>(() =>
                    {
                        short value = JsonTextParser.GetInt16Value(bufferedToken.Span);
                        return value;
                    });
                }

                public short Value
                {
                    get
                    {
                        return this.lazyValue.Value;
                    }
                }

                public static Int16Node Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    return new Int16Node(bufferedToken);
                }
            }

            private sealed class Int32Node : IntegerNode
            {
                private readonly Lazy<int> lazyValue;

                private Int32Node(ReadOnlyMemory<byte> bufferedToken)
                    : base(JsonNodeType.Int32)
                {
                    this.lazyValue = new Lazy<int>(() =>
                    {
                        int value = JsonTextParser.GetInt32Value(bufferedToken.Span);
                        return value;
                    });
                }

                public int Value
                {
                    get
                    {
                        return this.lazyValue.Value;
                    }
                }

                public static Int32Node Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    return new Int32Node(bufferedToken);
                }
            }

            private sealed class Int64Node : IntegerNode
            {
                private readonly Lazy<long> lazyValue;

                private Int64Node(ReadOnlyMemory<byte> bufferedToken)
                    : base(JsonNodeType.Int64)
                {
                    this.lazyValue = new Lazy<long>(() =>
                    {
                        long value = JsonTextParser.GetInt64Value(bufferedToken.Span);
                        return value;
                    });
                }

                public long Value
                {
                    get
                    {
                        return this.lazyValue.Value;
                    }
                }

                public static Int64Node Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    return new Int64Node(bufferedToken);
                }
            }

            private sealed class UInt32Node : IntegerNode
            {
                private readonly Lazy<uint> lazyValue;

                private UInt32Node(ReadOnlyMemory<byte> bufferedToken)
                    : base(JsonNodeType.UInt32)
                {
                    this.lazyValue = new Lazy<uint>(() =>
                    {
                        uint value = JsonTextParser.GetUInt32Value(bufferedToken.Span);
                        return value;
                    });
                }

                public uint Value
                {
                    get
                    {
                        return this.lazyValue.Value;
                    }
                }

                public static UInt32Node Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    return new UInt32Node(bufferedToken);
                }
            }

            private abstract class FloatNode : JsonTextNode
            {
                protected FloatNode(JsonNodeType jsonNodeType)
                    : base(jsonNodeType)
                {
                }
            }

            private sealed class Float32Node : FloatNode
            {
                private readonly Lazy<float> lazyValue;

                private Float32Node(ReadOnlyMemory<byte> bufferedToken)
                    : base(JsonNodeType.Float32)
                {
                    this.lazyValue = new Lazy<float>(() =>
                    {
                        float value = JsonTextParser.GetFloat32Value(bufferedToken.Span);
                        return value;
                    });
                }

                public float Value
                {
                    get
                    {
                        return this.lazyValue.Value;
                    }
                }

                public static Float32Node Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    return new Float32Node(bufferedToken);
                }
            }

            private sealed class Float64Node : FloatNode
            {
                private readonly Lazy<double> lazyValue;

                private Float64Node(ReadOnlyMemory<byte> bufferedToken)
                    : base(JsonNodeType.Float64)
                {
                    this.lazyValue = new Lazy<double>(() =>
                    {
                        double value = JsonTextParser.GetFloat64Value(bufferedToken.Span);
                        return value;
                    });
                }

                public double Value
                {
                    get
                    {
                        return this.lazyValue.Value;
                    }
                }

                public static Float64Node Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    return new Float64Node(bufferedToken);
                }
            }

            private sealed class GuidNode : JsonTextNode
            {
                private readonly Lazy<Guid> lazyValue;

                private GuidNode(ReadOnlyMemory<byte> bufferedToken)
                    : base(JsonNodeType.Guid)
                {
                    this.lazyValue = new Lazy<Guid>(() =>
                    {
                        Guid value = JsonTextParser.GetGuidValue(bufferedToken.Span);
                        return value;
                    });
                }

                public Guid Value
                {
                    get
                    {
                        return this.lazyValue.Value;
                    }
                }

                public static GuidNode Create(ReadOnlyMemory<byte> value)
                {
                    return new GuidNode(value);
                }
            }

            private sealed class BinaryNode : JsonTextNode
            {
                private readonly Lazy<ReadOnlyMemory<byte>> lazyValue;

                private BinaryNode(ReadOnlyMemory<byte> bufferedToken)
                    : base(JsonNodeType.Binary)
                {
                    this.lazyValue = new Lazy<ReadOnlyMemory<byte>>(() =>
                    {
                        return JsonTextParser.GetBinaryValue(bufferedToken.Span);
                    });
                }

                public ReadOnlyMemory<byte> Value
                {
                    get
                    {
                        return this.lazyValue.Value;
                    }
                }

                public static BinaryNode Create(ReadOnlyMemory<byte> value)
                {
                    return new BinaryNode(value);
                }
            }
            #endregion
        }
    }
}
