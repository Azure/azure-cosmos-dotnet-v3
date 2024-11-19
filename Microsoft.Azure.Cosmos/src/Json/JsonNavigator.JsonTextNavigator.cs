//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Xml.Linq;
    using Microsoft.Azure.Cosmos.Core.Utf8;

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
        /// </summary>
        private sealed class JsonTextNavigator : JsonNavigator
        {
            private static readonly Utf8Memory ReverseSoldius = Utf8Memory.Create("\\");

            private static class SingletonBuffers
            {
                public static readonly ReadOnlyMemory<byte> True = Encoding.UTF8.GetBytes("true");
                public static readonly ReadOnlyMemory<byte> False = Encoding.UTF8.GetBytes("false");
                public static readonly ReadOnlyMemory<byte> Null = Encoding.UTF8.GetBytes("null");
            }

            private readonly JsonTextNavigatorNode rootNode;

            /// <summary>
            /// Initializes a new instance of the <see cref="JsonTextNavigator"/> class.
            /// </summary>
            /// <param name="buffer">The (UTF-8) buffer to navigate.</param>
            public JsonTextNavigator(ReadOnlyMemory<byte> buffer)
            {
                if (buffer.IsEmpty)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(buffer)} can not be empty.");
                }

                byte firstByte = buffer.Span[0];
                byte lastByte = buffer.Span[buffer.Span.Length - 1];

                bool objectRoot = (firstByte == '{') && (lastByte == '}');
                bool arrayRoot = (firstByte == '[') && (lastByte == ']');

                bool lazyInit = objectRoot || arrayRoot;

                JsonTextNavigatorNode CreateRootNode()
                {
                    IJsonReader jsonTextReader = JsonReader.Create(buffer: buffer);
                    if (!(jsonTextReader is IJsonTextReaderPrivateImplementation jsonTextReaderExtensions))
                    {
                        throw new InvalidOperationException($"{nameof(jsonTextReader)} needs to implement {nameof(IJsonTextReaderPrivateImplementation)}.");
                    }

                    return Parser.Parse(jsonTextReaderExtensions);
                }

                JsonTextNavigatorNode rootNode;
                if (lazyInit)
                {
                    Lazy<JsonTextNavigatorNode> lazyNode = new Lazy<JsonTextNavigatorNode>(CreateRootNode);
                    if (arrayRoot)
                    {
                        rootNode = new LazyArrayNode(lazyNode, buffer);
                    }
                    else
                    {
                        rootNode = new LazyObjectNode(lazyNode, buffer);
                    }
                }
                else
                {
                    rootNode = CreateRootNode();
                }

                this.rootNode = rootNode;
            }

            #region IJsonNavigator
            /// <inheritdoc />
            public override JsonSerializationFormat SerializationFormat => JsonSerializationFormat.Text;

            /// <inheritdoc />
            public override IJsonNavigatorNode GetRootNode()
            {
                return this.rootNode;
            }

            /// <inheritdoc />
            public override JsonNodeType GetNodeType(IJsonNavigatorNode node)
            {
                if (!(node is JsonTextNavigatorNode jsonTextNavigatorNode))
                {
                    throw new ArgumentException("node must actually be a text node.");
                }

                return jsonTextNavigatorNode.Type;
            }

            /// <inheritdoc />
            public override Number64 GetNumberValue(IJsonNavigatorNode node)
            {
                if (!(node is NumberNode numberNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(NumberNode)}.");
                }

                Number64 value = JsonTextParser.GetNumberValue(numberNode.BufferedToken.Span);
                return value;
            }

            /// <inheritdoc />
            public override bool TryGetBufferedStringValue(
                IJsonNavigatorNode node,
                out Utf8Memory value)
            {
                if (!(node is StringNodeBase stringNodeBase))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(StringNodeBase)}.");
                }

                // TODO: consider cacheing whether or not the string is escaped in the node itself.
                if (stringNodeBase.BufferedValue.Span.Contains(ReverseSoldius.Span))
                {
                    // encountered escaped string that can't be returned
                    value = default;
                    return false;
                }

                // Just trim off the quotes
                value = stringNodeBase.BufferedValue.Slice(start: 1, length: stringNodeBase.BufferedValue.Length - 2);
                return true;
            }

            /// <inheritdoc />
            public override UtfAnyString GetStringValue(IJsonNavigatorNode node)
            {
                if (!(node is StringNodeBase stringNodeBase))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(StringNodeBase)}.");
                }

                return JsonTextParser.GetStringValue(stringNodeBase.BufferedValue);
            }

            /// <inheritdoc />
            public override sbyte GetInt8Value(IJsonNavigatorNode node)
            {
                if (!(node is Int8Node numberNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(Int8Node)}.");
                }

                sbyte value = JsonTextParser.GetInt8Value(numberNode.BufferedToken.Span);
                return value;
            }

            /// <inheritdoc />
            public override short GetInt16Value(IJsonNavigatorNode node)
            {
                if (!(node is Int16Node numberNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(Int16Node)}.");
                }

                short value = JsonTextParser.GetInt16Value(numberNode.BufferedToken.Span);
                return value;
            }

            /// <inheritdoc />
            public override int GetInt32Value(IJsonNavigatorNode node)
            {
                if (!(node is Int32Node numberNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(Int32Node)}.");
                }

                int value = JsonTextParser.GetInt32Value(numberNode.BufferedToken.Span);
                return value;
            }

            /// <inheritdoc />
            public override long GetInt64Value(IJsonNavigatorNode node)
            {
                if (!(node is Int64Node numberNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(Int64Node)}.");
                }

                long value = JsonTextParser.GetInt64Value(numberNode.BufferedToken.Span);
                return value;
            }

            /// <inheritdoc />
            public override float GetFloat32Value(IJsonNavigatorNode node)
            {
                if (!(node is Float32Node numberNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(Float32Node)}.");
                }

                float value = JsonTextParser.GetFloat32Value(numberNode.BufferedToken.Span);
                return value;
            }

            /// <inheritdoc />
            public override double GetFloat64Value(IJsonNavigatorNode node)
            {
                if (!(node is Float64Node numberNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(Float64Node)}.");
                }

                double value = JsonTextParser.GetFloat64Value(numberNode.BufferedToken.Span);
                return value;
            }

            /// <inheritdoc />
            public override uint GetUInt32Value(IJsonNavigatorNode node)
            {
                if (!(node is UInt32Node numberNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(UInt32Node)}.");
                }

                uint value = JsonTextParser.GetUInt32Value(numberNode.BufferedToken.Span);
                return value;
            }

            /// <inheritdoc />
            public override Guid GetGuidValue(IJsonNavigatorNode node)
            {
                if (!(node is GuidNode guidNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(GuidNode)}.");
                }

                Guid value = JsonTextParser.GetGuidValue(guidNode.BufferedToken.Span);
                return value;
            }

            /// <inheritdoc />
            public override ReadOnlyMemory<byte> GetBinaryValue(IJsonNavigatorNode node)
            {
                if (!(node is BinaryNode binaryNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(BinaryNode)}.");
                }

                ReadOnlyMemory<byte> value = JsonTextParser.GetBinaryValue(binaryNode.BufferedToken.Span);
                return value;
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
            public override int GetArrayItemCount(IJsonNavigatorNode node)
            {
                if (node is LazyNode lazyNode)
                {
                    node = lazyNode.Value;
                }

                if (!(node is ArrayNode arrayNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(ArrayNode)}.");
                }

                return arrayNode.Items.Count;
            }

            /// <inheritdoc />
            public override IJsonNavigatorNode GetArrayItemAt(IJsonNavigatorNode node, int index)
            {
                if (node is LazyNode lazyNode)
                {
                    node = lazyNode.Value;
                }

                if (!(node is ArrayNode arrayNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(ArrayNode)}.");
                }

                return arrayNode.Items[index];
            }

            /// <inheritdoc />
            public override IEnumerable<IJsonNavigatorNode> GetArrayItems(IJsonNavigatorNode node)
            {
                if (node is LazyNode lazyNode)
                {
                    node = lazyNode.Value;
                }

                if (!(node is ArrayNode arrayNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(ArrayNode)}.");
                }

                return arrayNode.Items;
            }

            /// <inheritdoc />
            public override int GetObjectPropertyCount(IJsonNavigatorNode node)
            {
                if (node is LazyNode lazyNode)
                {
                    node = lazyNode.Value;
                }

                if (!(node is ObjectNode objectNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(ObjectNode)}.");
                }

                return objectNode.Properties.Count;
            }

            /// <inheritdoc />
            public override bool TryGetObjectProperty(IJsonNavigatorNode node, string propertyName, out ObjectProperty objectProperty)
            {
                if (node is LazyNode lazyNode)
                {
                    node = lazyNode.Value;
                }

                if (!(node is ObjectNode objectNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(ObjectNode)}.");
                }

                Utf8Memory propertyNameAsUtf8 = Utf8Memory.Create(propertyName);

                foreach (ObjectProperty property in objectNode.Properties)
                {
                    if (!this.TryGetBufferedStringValue(property.NameNode, out Utf8Memory candidate))
                    {
                        throw new InvalidOperationException("Failed to get property name buffered value.");
                    }

                    if (propertyNameAsUtf8.Span == candidate.Span)
                    {
                        objectProperty = property;
                        return true;
                    }
                }

                objectProperty = default;
                return false;
            }

            /// <inheritdoc />
            public override IEnumerable<ObjectProperty> GetObjectProperties(IJsonNavigatorNode node)
            {
                if (node is LazyNode lazyNode)
                {
                    node = lazyNode.Value;
                }

                if (!(node is ObjectNode objectNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(ObjectNode)}.");
                }

                return objectNode.Properties;
            }

            public override void WriteNode(IJsonNavigatorNode jsonNavigatorNode, IJsonWriter jsonWriter)
            {
                if (!(jsonNavigatorNode is JsonTextNavigatorNode textNavigatorNode))
                {
                    throw new ArgumentOutOfRangeException($"Expected {nameof(jsonNavigatorNode)} to be a {nameof(JsonTextNavigatorNode)}.");
                }

                bool sameEncoding = this.SerializationFormat == jsonWriter.SerializationFormat;
                if (sameEncoding && jsonWriter is IJsonTextWriterExtensions jsonTextWriter)
                {
                    bool isFieldName = textNavigatorNode.Type == JsonNodeType.FieldName;
                    jsonTextWriter.WriteRawJsonValue(
                        JsonTextNavigator.GetNodeBuffer(textNavigatorNode),
                        isFieldName);
                }
                else
                {
                    // TODO: we can optimize this to use private APIs in the future, but for now defaulting to public API.
                    base.WriteNode(jsonNavigatorNode, jsonWriter);
                }
            }

            public override IJsonReader CreateReader(IJsonNavigatorNode node)
            {
                if (!(node is JsonTextNavigatorNode jsonTextNavigatorNode))
                {
                    throw new ArgumentException($"{nameof(node)} must be a {nameof(JsonTextNavigatorNode)}.");
                }

                ReadOnlyMemory<byte> buffer = JsonTextNavigator.GetNodeBuffer(jsonTextNavigatorNode);
                return JsonReader.Create(JsonSerializationFormat.Text, buffer);
            }
            #endregion

            protected override bool TryGetUInt64Value(IJsonNavigatorNode node, out ulong value)
            {
                if (!(node is NumberNode numberNode))
                {
                    throw new ArgumentException($"{node} was not of type: {nameof(NumberNode)}.");
                }

                return JsonTextParser.TryGetUInt64Value(numberNode.BufferedToken.Span, out value);
            }

            private static ReadOnlyMemory<byte> GetNodeBuffer(JsonTextNavigatorNode jsonTextNavigatorNode)
            {
                return jsonTextNavigatorNode switch
                {
                    LazyNode lazyNode => lazyNode.BufferedValue,
                    ArrayNode arrayNode => arrayNode.BufferedValue,
                    FalseNode falseNode => SingletonBuffers.False,
                    StringNodeBase stringNodeBase => stringNodeBase.BufferedValue.Memory,
                    NullNode nullNode => SingletonBuffers.Null,
                    NumberNode numberNode => numberNode.BufferedToken,
                    ObjectNode objectNode => objectNode.BufferedValue,
                    TrueNode trueNode => SingletonBuffers.True,
                    GuidNode guidNode => guidNode.BufferedToken,
                    BinaryNode binaryNode => binaryNode.BufferedToken,
                    IntegerNode intNode => intNode.BufferedToken,
                    FloatNode floatNode => floatNode.BufferedToken,
                    _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(JsonTextNavigatorNode)} type: {jsonTextNavigatorNode.GetType()}."),
                };
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
                public static JsonTextNavigatorNode Parse(IJsonTextReaderPrivateImplementation jsonTextReader)
                {
                    // Read past the json object not started state.
                    if (!jsonTextReader.Read())
                    {
                        throw new InvalidOperationException("Failed to read from reader");
                    }

                    JsonTextNavigatorNode rootNode = Parser.ParseNode(jsonTextReader);

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
                private static ArrayNode ParseArrayNode(IJsonTextReaderPrivateImplementation jsonTextReader)
                {
                    List<JsonTextNavigatorNode> items = new List<JsonTextNavigatorNode>();

                    ReadOnlyMemory<byte> bufferedArrayStartToken = jsonTextReader.GetBufferedJsonToken().Memory;
                    if (!MemoryMarshal.TryGetArray(bufferedArrayStartToken, out ArraySegment<byte> startArrayArraySegment))
                    {
                        throw new InvalidOperationException($"Failed to get {nameof(startArrayArraySegment)}.");
                    }

                    // consume the begin array token
                    jsonTextReader.Read();

                    while (jsonTextReader.CurrentTokenType != JsonTokenType.EndArray)
                    {
                        items.Add(Parser.ParseNode(jsonTextReader));
                    }

                    ReadOnlyMemory<byte> bufferedArrayEndToken = jsonTextReader.GetBufferedJsonToken().Memory;
                    if (!MemoryMarshal.TryGetArray(bufferedArrayEndToken, out ArraySegment<byte> endArrayArraySegment))
                    {
                        throw new InvalidOperationException($"Failed to get {nameof(endArrayArraySegment)}.");
                    }

                    // consume the end array token
                    jsonTextReader.Read();

                    ReadOnlyMemory<byte> bufferedRawArray = startArrayArraySegment.Array;
                    bufferedRawArray = bufferedRawArray.Slice(start: startArrayArraySegment.Offset, length: endArrayArraySegment.Offset - startArrayArraySegment.Offset + 1);

                    return ArrayNode.Create(items, bufferedRawArray);
                }

                /// <summary>
                /// Parses out a JSON object AST node with a jsonTextReader.
                /// </summary>
                /// <param name="jsonTextReader">The reader to use as a lexer / tokenizer</param>
                /// <returns>JSON object AST node</returns>
                private static ObjectNode ParseObjectNode(IJsonTextReaderPrivateImplementation jsonTextReader)
                {
                    List<ObjectProperty> properties = new List<ObjectProperty>();

                    ReadOnlyMemory<byte> bufferedObjectStartToken = jsonTextReader.GetBufferedJsonToken().Memory;
                    if (!MemoryMarshal.TryGetArray(bufferedObjectStartToken, out ArraySegment<byte> startObjectArraySegment))
                    {
                        throw new InvalidOperationException($"Failed to get {nameof(startObjectArraySegment)}.");
                    }

                    // consume the begin object token
                    jsonTextReader.Read();

                    while (jsonTextReader.CurrentTokenType != JsonTokenType.EndObject)
                    {
                        ObjectProperty property = Parser.ParsePropertyNode(jsonTextReader);
                        properties.Add(property);
                    }

                    ReadOnlyMemory<byte> bufferedObjectEndToken = jsonTextReader.GetBufferedJsonToken().Memory;
                    if (!MemoryMarshal.TryGetArray(bufferedObjectEndToken, out ArraySegment<byte> endObjectArraySegment))
                    {
                        throw new InvalidOperationException($"Failed to get {nameof(endObjectArraySegment)}.");
                    }

                    // consume the end object token
                    jsonTextReader.Read();

                    ReadOnlyMemory<byte> bufferedRawObject = startObjectArraySegment.Array;
                    bufferedRawObject = bufferedRawObject.Slice(start: startObjectArraySegment.Offset, length: endObjectArraySegment.Offset - startObjectArraySegment.Offset + 1);

                    return ObjectNode.Create(properties, bufferedRawObject);
                }

                /// <summary>
                /// Parses out a JSON string AST node with a jsonTextReader.
                /// </summary>
                /// <param name="jsonTextReader">The reader to use as a lexer / tokenizer</param>
                /// <returns>JSON string AST node</returns>
                private static StringNode ParseStringNode(IJsonTextReaderPrivateImplementation jsonTextReader)
                {
                    Utf8Memory bufferedStringValue = jsonTextReader.GetBufferedJsonToken();
                    StringNode stringNode = StringNode.Create(bufferedStringValue);

                    // consume the string from the reader
                    jsonTextReader.Read();

                    return stringNode;
                }

                /// <summary>
                /// Parses out a JSON number AST node with a jsonTextReader.
                /// </summary>
                /// <param name="jsonTextReader">The reader to use as a lexer / tokenizer</param>
                /// <returns>JSON number AST node</returns>
                private static NumberNode ParseNumberNode(IJsonTextReaderPrivateImplementation jsonTextReader)
                {
                    ReadOnlyMemory<byte> bufferedRawJsonToken = jsonTextReader.GetBufferedJsonToken().Memory;
                    NumberNode numberNode = NumberNode.Create(bufferedRawJsonToken);

                    // consume the number from the reader
                    jsonTextReader.Read();

                    return numberNode;
                }

                private static IntegerNode ParseIntegerNode(
                    IJsonTextReaderPrivateImplementation jsonTextReader,
                    JsonTokenType jsonTokenType)
                {
                    ReadOnlyMemory<byte> bufferedRawJsonToken = jsonTextReader.GetBufferedJsonToken().Memory;
                    IntegerNode integerNode = jsonTokenType switch
                    {
                        JsonTokenType.Int8 => Int8Node.Create(bufferedRawJsonToken),
                        JsonTokenType.Int16 => Int16Node.Create(bufferedRawJsonToken),
                        JsonTokenType.Int32 => Int32Node.Create(bufferedRawJsonToken),
                        JsonTokenType.Int64 => Int64Node.Create(bufferedRawJsonToken),
                        JsonTokenType.UInt32 => UInt32Node.Create(bufferedRawJsonToken),
                        _ => throw new ArgumentException($"Unknown {nameof(JsonTokenType)}: {jsonTokenType}"),
                    };

                    // consume the integer from the reader
                    jsonTextReader.Read();

                    return integerNode;
                }

                private static FloatNode ParseFloatNode(
                    IJsonTextReaderPrivateImplementation jsonTextReader,
                    JsonTokenType jsonTokenType)
                {
                    ReadOnlyMemory<byte> bufferedRawJsonToken = jsonTextReader.GetBufferedJsonToken().Memory;
                    FloatNode floatNode = jsonTokenType switch
                    {
                        JsonTokenType.Float32 => Float32Node.Create(bufferedRawJsonToken),
                        JsonTokenType.Float64 => Float64Node.Create(bufferedRawJsonToken),
                        _ => throw new ArgumentException($"Unknown {nameof(JsonTokenType)}: {jsonTokenType}"),
                    };

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
                private static ObjectProperty ParsePropertyNode(IJsonTextReaderPrivateImplementation jsonTextReader)
                {
                    ReadOnlyMemory<byte> bufferedRawJsonToken = jsonTextReader.GetBufferedJsonToken().Memory;
                    FieldNameNode fieldName = FieldNameNode.Create(Utf8Memory.UnsafeCreateNoValidation(bufferedRawJsonToken));

                    // Consume the fieldname from the jsonreader
                    jsonTextReader.Read();

                    JsonTextNavigatorNode value = Parser.ParseNode(jsonTextReader);
                    return new ObjectProperty(fieldName, value);
                }

                private static GuidNode ParseGuidNode(IJsonTextReaderPrivateImplementation jsonTextReader)
                {
                    ReadOnlyMemory<byte> bufferedRawJsonToken = jsonTextReader.GetBufferedJsonToken().Memory;
                    GuidNode node = GuidNode.Create(bufferedRawJsonToken);

                    // advance the reader forward.
                    jsonTextReader.Read();
                    return node;
                }

                private static BinaryNode ParseBinaryNode(IJsonTextReaderPrivateImplementation jsonTextReader)
                {
                    ReadOnlyMemory<byte> bufferedRawJsonToken = jsonTextReader.GetBufferedJsonToken().Memory;
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
                private static JsonTextNavigatorNode ParseNode(IJsonTextReaderPrivateImplementation jsonTextReader)
                {
                    JsonTextNavigatorNode node;
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
            private abstract class JsonTextNavigatorNode : IJsonNavigatorNode
            {
                protected JsonTextNavigatorNode()
                {
                }

                public abstract JsonNodeType Type { get; }
            }

            private sealed class ArrayNode : JsonTextNavigatorNode
            {
                private ArrayNode(
                    IReadOnlyList<JsonTextNavigatorNode> items,
                    ReadOnlyMemory<byte> bufferedValue)
                {
                    this.Items = items;
                    this.BufferedValue = bufferedValue;
                }

                public IReadOnlyList<JsonTextNavigatorNode> Items { get; }
                public ReadOnlyMemory<byte> BufferedValue { get; }

                public override JsonNodeType Type => JsonNodeType.Array;

                public static ArrayNode Create(
                    IReadOnlyList<JsonTextNavigatorNode> items,
                    ReadOnlyMemory<byte> bufferedValue)
                {
                    return new ArrayNode(items, bufferedValue);
                }
            }

            private sealed class FalseNode : JsonTextNavigatorNode
            {
                private static readonly FalseNode Instance = new FalseNode();

                private FalseNode()
                {
                }

                public override JsonNodeType Type => JsonNodeType.False;

                public static FalseNode Create()
                {
                    return FalseNode.Instance;
                }
            }

            private sealed class FieldNameNode : StringNodeBase
            {
                private static readonly FieldNameNode Empty = new FieldNameNode(Utf8Memory.Empty);

                private FieldNameNode(Utf8Memory bufferedValue)
                    : base(bufferedValue)
                {
                }

                public override JsonNodeType Type => JsonNodeType.FieldName;

                public static FieldNameNode Create(Utf8Memory bufferedToken)
                {
                    if (bufferedToken.Length == 0)
                    {
                        return FieldNameNode.Empty;
                    }

                    // In the future we can have a flyweight dictionary for system strings.
                    return new FieldNameNode(bufferedToken);
                }
            }

            private sealed class NullNode : JsonTextNavigatorNode
            {
                private static readonly NullNode Instance = new NullNode();

                private NullNode()
                {
                }

                public override JsonNodeType Type => JsonNodeType.Null;

                public static NullNode Create()
                {
                    return NullNode.Instance;
                }
            }

            private sealed class NumberNode : JsonTextNavigatorNode
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

                private NumberNode(ReadOnlyMemory<byte> bufferedToken)
                {
                    this.BufferedToken = bufferedToken;
                }

                private NumberNode(int value)
                    : this(Encoding.UTF8.GetBytes(value.ToString()))
                {
                }

                public ReadOnlyMemory<byte> BufferedToken { get; }

                public override JsonNodeType Type => JsonNodeType.Number;

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

            private sealed class ObjectNode : JsonTextNavigatorNode
            {
                private ObjectNode(IReadOnlyList<ObjectProperty> properties, ReadOnlyMemory<byte> bufferedValue)
                {
                    this.Properties = properties;
                    this.BufferedValue = bufferedValue;
                }

                public IReadOnlyList<ObjectProperty> Properties { get; }
                public ReadOnlyMemory<byte> BufferedValue { get; }

                public override JsonNodeType Type => JsonNodeType.Object;

                public static ObjectNode Create(
                    IReadOnlyList<ObjectProperty> properties,
                    ReadOnlyMemory<byte> bufferedValue)
                {
                    return new ObjectNode(properties, bufferedValue);
                }
            }

            private sealed class StringNode : StringNodeBase
            {
                private static readonly StringNode Empty = new StringNode(Utf8Memory.Empty);

                private StringNode(Utf8Memory bufferedValue)
                    : base(bufferedValue)
                {
                }

                public override JsonNodeType Type => JsonNodeType.String;

                public static StringNode Create(Utf8Memory bufferedToken)
                {
                    if (bufferedToken.Length == 0)
                    {
                        return StringNode.Empty;
                    }

                    // In the future we can have a flyweight dictionary for system strings.
                    return new StringNode(bufferedToken);
                }
            }

            private abstract class StringNodeBase : JsonTextNavigatorNode
            {
                protected StringNodeBase(
                    Utf8Memory bufferedValue)
                {
                    this.BufferedValue = bufferedValue;
                }

                public Utf8Memory BufferedValue { get; }
            }

            private sealed class TrueNode : JsonTextNavigatorNode
            {
                private static readonly TrueNode Instance = new TrueNode();

                private TrueNode()
                {
                }

                public override JsonNodeType Type => JsonNodeType.True;

                public static TrueNode Create()
                {
                    return TrueNode.Instance;
                }
            }

            private abstract class IntegerNode : JsonTextNavigatorNode
            {
                protected IntegerNode(ReadOnlyMemory<byte> bufferedToken)
                {
                    this.BufferedToken = bufferedToken;
                }

                public ReadOnlyMemory<byte> BufferedToken { get; }
            }

            private sealed class Int8Node : IntegerNode
            {
                private Int8Node(ReadOnlyMemory<byte> bufferedToken)
                    : base(bufferedToken)
                {
                }

                public override JsonNodeType Type => JsonNodeType.Int8;

                public static Int8Node Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    return new Int8Node(bufferedToken);
                }
            }

            private sealed class Int16Node : IntegerNode
            {
                private Int16Node(ReadOnlyMemory<byte> bufferedToken)
                    : base(bufferedToken)
                {
                }

                public override JsonNodeType Type => JsonNodeType.Int16;

                public static Int16Node Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    return new Int16Node(bufferedToken);
                }
            }

            private sealed class Int32Node : IntegerNode
            {
                private Int32Node(ReadOnlyMemory<byte> bufferedToken)
                    : base(bufferedToken)
                {
                }

                public override JsonNodeType Type => JsonNodeType.Int32;

                public static Int32Node Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    return new Int32Node(bufferedToken);
                }
            }

            private sealed class Int64Node : IntegerNode
            {
                private Int64Node(ReadOnlyMemory<byte> bufferedToken)
                     : base(bufferedToken)
                {
                }

                public override JsonNodeType Type => JsonNodeType.Int64;

                public static Int64Node Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    return new Int64Node(bufferedToken);
                }
            }

            private sealed class UInt32Node : IntegerNode
            {
                private UInt32Node(ReadOnlyMemory<byte> bufferedToken)
                     : base(bufferedToken)
                {
                }

                public override JsonNodeType Type => JsonNodeType.UInt32;

                public static UInt32Node Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    return new UInt32Node(bufferedToken);
                }
            }

            private abstract class FloatNode : JsonTextNavigatorNode
            {
                protected FloatNode(ReadOnlyMemory<byte> bufferedToken)
                {
                    this.BufferedToken = bufferedToken;
                }

                public ReadOnlyMemory<byte> BufferedToken { get; }
            }

            private sealed class Float32Node : FloatNode
            {
                private Float32Node(ReadOnlyMemory<byte> bufferedToken)
                     : base(bufferedToken)
                {
                }

                public override JsonNodeType Type => JsonNodeType.Float32;

                public static Float32Node Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    return new Float32Node(bufferedToken);
                }
            }

            private sealed class Float64Node : FloatNode
            {
                private Float64Node(ReadOnlyMemory<byte> bufferedToken)
                     : base(bufferedToken)
                {
                }

                public override JsonNodeType Type => JsonNodeType.Float64;

                public static Float64Node Create(ReadOnlyMemory<byte> bufferedToken)
                {
                    return new Float64Node(bufferedToken);
                }
            }

            private sealed class GuidNode : JsonTextNavigatorNode
            {
                private GuidNode(ReadOnlyMemory<byte> bufferedToken)
                {
                    this.BufferedToken = bufferedToken;
                }

                public ReadOnlyMemory<byte> BufferedToken { get; }

                public override JsonNodeType Type => JsonNodeType.Guid;

                public static GuidNode Create(ReadOnlyMemory<byte> value)
                {
                    return new GuidNode(value);
                }
            }

            private sealed class BinaryNode : JsonTextNavigatorNode
            {
                private BinaryNode(ReadOnlyMemory<byte> bufferedToken)
                {
                    this.BufferedToken = bufferedToken;
                }

                public ReadOnlyMemory<byte> BufferedToken { get; }

                public override JsonNodeType Type => JsonNodeType.Binary;

                public static BinaryNode Create(ReadOnlyMemory<byte> value)
                {
                    return new BinaryNode(value);
                }
            }

            private abstract class LazyNode : JsonTextNavigatorNode
            {
                private readonly Lazy<JsonTextNavigatorNode> lazyNode;

                protected LazyNode(
                    Lazy<JsonTextNavigatorNode> lazyNode,
                    ReadOnlyMemory<byte> bufferedValue)
                {
                    this.lazyNode = lazyNode;
                    this.BufferedValue = bufferedValue;
                }

                public ReadOnlyMemory<byte> BufferedValue { get; }

                public JsonTextNavigatorNode Value => this.lazyNode.Value;
            }

            private sealed class LazyArrayNode : LazyNode
            {
                public LazyArrayNode(
                    Lazy<JsonTextNavigatorNode> lazyNode,
                    ReadOnlyMemory<byte> bufferedValue)
                    : base(lazyNode, bufferedValue)
                {
                }

                public override JsonNodeType Type => JsonNodeType.Array;
            }

            private sealed class LazyObjectNode : LazyNode
            {
                public LazyObjectNode(
                    Lazy<JsonTextNavigatorNode> lazyNode,
                    ReadOnlyMemory<byte> bufferedValue)
                    : base(lazyNode, bufferedValue)
                {
                }

                public override JsonNodeType Type => JsonNodeType.Object;
            }
            #endregion
        }
    }
}
