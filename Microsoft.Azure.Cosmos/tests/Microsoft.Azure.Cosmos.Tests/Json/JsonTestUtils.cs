namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    internal class JsonTestUtils
    {
        public static byte[] ConvertTextToBinary(string text)
        {
            IJsonWriter binaryWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
            IJsonReader textReader = JsonReader.Create(Encoding.UTF8.GetBytes(text));
            textReader.WriteAll(binaryWriter);
            return binaryWriter.GetResult().ToArray();
        }

        public static string ConvertBinaryToText(ReadOnlyMemory<byte> binary)
        {
            IJsonReader binaryReader = JsonReader.Create(binary);
            IJsonWriter textWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            binaryReader.WriteAll(textWriter);
            return Encoding.UTF8.GetString(textWriter.GetResult().ToArray());
        }

        public static string LoadJsonCuratedDocument(string filename)
        {
            string path = string.Format("TestJsons/{0}", filename);
            return TextFileConcatenation.ReadMultipartFile(path);
        }

        public static string RandomSampleJson(
            string inputJson,
            int maxNumberOfItems,
            int? seed = null)
        {
            string sampledJson;

            IJsonNavigator navigator = JsonNavigator.Create(Encoding.UTF8.GetBytes(inputJson));

            IJsonNavigatorNode rootNode = navigator.GetRootNode();
            JsonNodeType rootNodeType = navigator.GetNodeType(rootNode);
            if (rootNodeType == JsonNodeType.Array)
            {
                if (navigator.GetArrayItemCount(rootNode) > maxNumberOfItems)
                {
                    Random random = seed.HasValue ? new Random(seed.Value) : new Random();

                    IJsonNavigatorNode[] arrayItems = navigator.GetArrayItems(rootNode).ToArray();
                    IJsonNavigatorNode[] randomArrayItems = new IJsonNavigatorNode[maxNumberOfItems];

                    HashSet<int> uniqueIndexes = new HashSet<int>();

                    int count = 0;
                    while (count < maxNumberOfItems)
                    {
                        int index = random.Next(arrayItems.Length);
                        if (!uniqueIndexes.Contains(index))
                        {
                            randomArrayItems[count++] = arrayItems[index];
                            uniqueIndexes.Add(index);
                        }
                    }

                    IJsonWriter writer = JsonWriter.Create(JsonSerializationFormat.Text);

                    writer.WriteArrayStart();
                    foreach (IJsonNavigatorNode item in randomArrayItems)
                    {
                        navigator.WriteNode(item, writer);
                    }
                    writer.WriteArrayEnd();

                    sampledJson = Encoding.UTF8.GetString(writer.GetResult().ToArray());
                }
                else
                {
                    sampledJson = inputJson;
                }
            }
            else
            {
                sampledJson = inputJson;
            }

            return sampledJson;
        }

        public static JsonToken[] ReadJsonDocument(string inputJson)
        {
            IJsonReader reader = JsonReader.Create(Encoding.UTF8.GetBytes(inputJson));
            return ReadJsonDocument(reader);
        }

        public static JsonToken[] ReadJsonDocument(IJsonReader reader)
        {
            List<JsonToken> tokens = new List<JsonToken>();
            while (reader.Read())
            {
                JsonToken token = reader.CurrentTokenType switch
                {
                    JsonTokenType.NotStarted => throw new InvalidOperationException(),
                    JsonTokenType.BeginArray => JsonToken.ArrayStart(),
                    JsonTokenType.EndArray => JsonToken.ArrayEnd(),
                    JsonTokenType.BeginObject => JsonToken.ObjectStart(),
                    JsonTokenType.EndObject => JsonToken.ObjectEnd(),
                    JsonTokenType.String => JsonToken.String(reader.GetStringValue()),
                    JsonTokenType.Number => JsonToken.Number(reader.GetNumberValue()),
                    JsonTokenType.True => JsonToken.Boolean(true),
                    JsonTokenType.False => JsonToken.Boolean(false),
                    JsonTokenType.Null => JsonToken.Null(),
                    JsonTokenType.FieldName => JsonToken.FieldName(reader.GetStringValue()),
                    JsonTokenType.Int8 => JsonToken.Int8(reader.GetInt8Value()),
                    JsonTokenType.Int16 => JsonToken.Int16(reader.GetInt16Value()),
                    JsonTokenType.Int32 => JsonToken.Int32(reader.GetInt32Value()),
                    JsonTokenType.Int64 => JsonToken.Int64(reader.GetInt64Value()),
                    JsonTokenType.UInt32 => JsonToken.UInt32(reader.GetUInt32Value()),
                    JsonTokenType.Float32 => JsonToken.Float32(reader.GetFloat32Value()),
                    JsonTokenType.Float64 => JsonToken.Float64(reader.GetFloat64Value()),
                    JsonTokenType.Guid => JsonToken.Guid(reader.GetGuidValue()),
                    JsonTokenType.Binary => JsonToken.Binary(reader.GetBinaryValue()),
                    _ => throw new ArgumentException($"Unknown {nameof(JsonTokenType)}: {reader.CurrentTokenType}"),
                };
                tokens.Add(token);
            }

            return tokens.ToArray();
        }

        public static void WriteTokens(
            JsonToken[] tokensToWrite,
            IJsonWriter jsonWriter,
            bool writeAsUtf8String)
        {
            Assert.IsNotNull(tokensToWrite);
            Assert.IsNotNull(jsonWriter);

            foreach (JsonToken token in tokensToWrite)
            {
                if (token.IsNumberArray)
                {
                    switch (token.JsonTokenType)
                    {
                        case JsonTokenType.UInt8:
                            jsonWriter.WriteNumberArray((token as JsonNumberArrayToken<byte>).Values);
                            break;

                        case JsonTokenType.Int8:
                            jsonWriter.WriteNumberArray((token as JsonNumberArrayToken<sbyte>).Values);
                            break;

                        case JsonTokenType.Int16:
                            jsonWriter.WriteNumberArray((token as JsonNumberArrayToken<short>).Values);
                            break;

                        case JsonTokenType.Int32:
                            jsonWriter.WriteNumberArray((token as JsonNumberArrayToken<int>).Values);
                            break;

                        case JsonTokenType.Int64:
                            jsonWriter.WriteNumberArray((token as JsonNumberArrayToken<long>).Values);
                            break;

                        case JsonTokenType.Float32:
                            jsonWriter.WriteNumberArray((token as JsonNumberArrayToken<float>).Values);
                            break;

                        case JsonTokenType.Float64:
                            jsonWriter.WriteNumberArray((token as JsonNumberArrayToken<double>).Values);
                            break;
                        default:
                            Assert.Fail($"Unexpected number array JsonTokenType: {token.JsonTokenType}.");
                            break;
                    }
                }
                else
                {
                    switch (token.JsonTokenType)
                    {
                        case JsonTokenType.BeginArray:
                            jsonWriter.WriteArrayStart();
                            break;

                        case JsonTokenType.EndArray:
                            jsonWriter.WriteArrayEnd();
                            break;

                        case JsonTokenType.BeginObject:
                            jsonWriter.WriteObjectStart();
                            break;

                        case JsonTokenType.EndObject:
                            jsonWriter.WriteObjectEnd();
                            break;

                        case JsonTokenType.String:
                            string stringValue = (token as JsonStringToken).Value;
                            if (writeAsUtf8String)
                            {
                                jsonWriter.WriteStringValue(Utf8Span.TranscodeUtf16(stringValue));
                            }
                            else
                            {
                                jsonWriter.WriteStringValue(stringValue);
                            }
                            break;

                        case JsonTokenType.Number:
                            Number64 numberValue = (token as JsonNumberToken).Value;
                            jsonWriter.WriteNumber64Value(numberValue);
                            break;

                        case JsonTokenType.True:
                            jsonWriter.WriteBoolValue(true);
                            break;

                        case JsonTokenType.False:
                            jsonWriter.WriteBoolValue(false);
                            break;

                        case JsonTokenType.Null:
                            jsonWriter.WriteNullValue();
                            break;

                        case JsonTokenType.FieldName:
                            string fieldNameValue = (token as JsonFieldNameToken).Value;
                            if (writeAsUtf8String)
                            {
                                jsonWriter.WriteFieldName(Utf8Span.TranscodeUtf16(fieldNameValue));
                            }
                            else
                            {
                                jsonWriter.WriteFieldName(fieldNameValue);
                            }
                            break;

                        case JsonTokenType.Int8:
                            sbyte int8Value = (token as JsonInt8Token).Value;
                            jsonWriter.WriteInt8Value(int8Value);
                            break;

                        case JsonTokenType.Int16:
                            short int16Value = (token as JsonInt16Token).Value;
                            jsonWriter.WriteInt16Value(int16Value);
                            break;

                        case JsonTokenType.Int32:
                            int int32Value = (token as JsonInt32Token).Value;
                            jsonWriter.WriteInt32Value(int32Value);
                            break;

                        case JsonTokenType.Int64:
                            long int64Value = (token as JsonInt64Token).Value;
                            jsonWriter.WriteInt64Value(int64Value);
                            break;

                        case JsonTokenType.UInt32:
                            uint uint32Value = (token as JsonUInt32Token).Value;
                            jsonWriter.WriteUInt32Value(uint32Value);
                            break;

                        case JsonTokenType.Float32:
                            float float32Value = (token as JsonFloat32Token).Value;
                            jsonWriter.WriteFloat32Value(float32Value);
                            break;

                        case JsonTokenType.Float64:
                            double float64Value = (token as JsonFloat64Token).Value;
                            jsonWriter.WriteFloat64Value(float64Value);
                            break;

                        case JsonTokenType.Guid:
                            Guid guidValue = (token as JsonGuidToken).Value;
                            jsonWriter.WriteGuidValue(guidValue);
                            break;

                        case JsonTokenType.Binary:
                            ReadOnlyMemory<byte> binaryValue = (token as JsonBinaryToken).Value;
                            jsonWriter.WriteBinaryValue(binaryValue.Span);
                            break;

                        case JsonTokenType.NotStarted:
                        default:
                            Assert.Fail(string.Format("Got an unexpected JsonTokenType: {0} as an expected token type", token.JsonTokenType));
                            break;
                    }
                }
            }
        }

        public static RoundTripResult VerifyJsonRoundTrip(
            ReadOnlyMemory<byte> inputResult,
            string inputJson,
            SerializationSpec inputSpec,
            SerializationSpec outputSpec,
            RewriteScenario rewriteScenario,
            ReadOnlyMemory<byte> expectedOutputResult = default,
            Func<string, IJsonNavigator> newtonsoftNavigatorCreate = default)
        {
            Func<SerializationSpec, IJsonReader> createReader = (SerializationSpec spec) => spec.IsNewtonsoft ?
                NewtonsoftToCosmosDBReader.CreateFromString(inputJson) :
                JsonReader.Create(inputResult);

            Func<SerializationSpec, IJsonNavigator> createNavigator = (SerializationSpec spec) => spec.IsNewtonsoft ?
                (newtonsoftNavigatorCreate != null ? newtonsoftNavigatorCreate(inputJson) : null) :
                JsonNavigator.Create(inputResult);

            Func<SerializationSpec, IJsonWriter> createWriter = (SerializationSpec spec) => spec.IsNewtonsoft ?
                NewtonsoftToCosmosDBWriter.CreateTextWriter() :
                JsonWriter.Create(spec.SerializationFormat, spec.WriteOptions);

            Stopwatch timer = Stopwatch.StartNew();

            IJsonWriter outputWriter = createWriter(outputSpec);
            switch (rewriteScenario)
            {
                case RewriteScenario.NavigatorRoot:
                    {
                        IJsonNavigator inputNavigator = createNavigator(inputSpec);
                        inputNavigator.WriteNode(inputNavigator.GetRootNode(), outputWriter);
                    }
                    break;

                case RewriteScenario.NavigatorNode:
                    {
                        IJsonNavigator inputNavigator = createNavigator(inputSpec);
                        IJsonNavigatorNode rootNode = inputNavigator.GetRootNode();
                        JsonNodeType nodeType = inputNavigator.GetNodeType(rootNode);
                        switch (nodeType)
                        {
                            case JsonNodeType.Array:
                                outputWriter.WriteArrayStart();

                                foreach (IJsonNavigatorNode arrayItem in inputNavigator.GetArrayItems(rootNode))
                                {
                                    inputNavigator.WriteNode(arrayItem, outputWriter);
                                }

                                outputWriter.WriteArrayEnd();
                                break;

                            case JsonNodeType.Object:
                                outputWriter.WriteObjectStart();

                                foreach (ObjectProperty objectProperty in inputNavigator.GetObjectProperties(rootNode))
                                {
                                    inputNavigator.WriteNode(objectProperty.NameNode, outputWriter);
                                    inputNavigator.WriteNode(objectProperty.ValueNode, outputWriter);
                                }

                                outputWriter.WriteObjectEnd();
                                break;

                            default:
                                inputNavigator.WriteNode(inputNavigator.GetRootNode(), outputWriter);
                                break;
                        }
                    }
                    break;

                case RewriteScenario.ReaderAll:
                    {
                        IJsonReader inputReader = createReader(inputSpec);
                        inputReader.WriteAll(outputWriter);
                    }
                    break;

                case RewriteScenario.ReaderToken:
                    {
                        IJsonReader inputReader = createReader(inputSpec);
                        while (inputReader.Read())
                        {
                            inputReader.WriteCurrentToken(outputWriter);
                        }
                    }
                    break;

                default:
                    throw new ArgumentException($"Unexpected {nameof(rewriteScenario)} of type: {rewriteScenario}");
            }

            long executionTime = timer.ElapsedMilliseconds;

            // Compare input to output result
            ReadOnlyMemory<byte> outputResult = outputWriter.GetResult();

            byte[] inputBytes = inputResult.ToArray();
            byte[] outputBytes = outputResult.ToArray();
            byte[] expectedOutputBytes = expectedOutputResult.ToArray();

            StringBuilder verboseOutput = new StringBuilder();
            if (!outputBytes.SequenceEqual(expectedOutputBytes) &&
                !CompareResults(inputBytes, outputBytes, verboseWriter: new StringWriter(verboseOutput)))
            {
                string[] inputTextLines = SerializeResultBuffer(inputBytes, inputSpec.SerializationFormat);
                string[] outputTextLines = SerializeResultBuffer(outputBytes, outputSpec.SerializationFormat);

                Console.WriteLine($"Rewriting JSON document failed for rewrite scenario '{rewriteScenario}'.");
                Console.WriteLine();
                Console.WriteLine($"  Input Format        : {inputSpec.SerializationFormatToString()}");
                Console.WriteLine($"  Input Write Options : {inputSpec.WriteOptions}");
                Console.WriteLine();
                Console.WriteLine($"  Output Format       : {outputSpec.SerializationFormatToString()}");
                Console.WriteLine($"  Output Write Options: {outputSpec.WriteOptions}");
                Console.WriteLine();
                Console.WriteLine($"Comparison Errors:");
                Console.WriteLine(verboseOutput.ToString());

                Console.WriteLine();
                Console.WriteLine("Input Result:");
                foreach (string line in inputTextLines) Console.WriteLine(line);
                Console.WriteLine();
                Console.WriteLine("Output Result:");
                foreach (string line in outputTextLines) Console.WriteLine(line);

                Assert.Fail();
            }

            long verificationTime = timer.ElapsedMilliseconds - executionTime;

            return new RoundTripResult(outputResult, executionTime, verificationTime);
        }

        public static bool CompareResults(
            byte[] resultBuffer1,
            byte[] resultBuffer2,
            TextWriter verboseWriter = null)
        {
            Assert.IsNotNull(resultBuffer1);
            Assert.IsNotNull(resultBuffer2);

            // Fast check for identical buffers
            if (resultBuffer1.Equals(resultBuffer2)) return true;

            IJsonReader reader1 = JsonReader.Create(resultBuffer1);
            IJsonReader reader2 = JsonReader.Create(resultBuffer2);

            int tokenCount = 0;
            while (true)
            {
                bool read1 = reader1.Read();
                bool read2 = reader2.Read();

                if (read1 != read2)
                {
                    if (verboseWriter != null)
                    {
                        verboseWriter.WriteLine($"Read method return value mismatch at token number {tokenCount}");
                        verboseWriter.WriteLine($"  Return Value 1: {read1}");
                        verboseWriter.WriteLine($"  Return Value 2: {read2}");
                    }

                    return false;
                }

                // If EOF, exit the while loop
                if (!read1) break;

                tokenCount++;

                JsonTokenType tokenType1 = reader1.CurrentTokenType;
                JsonTokenType tokenType2 = reader2.CurrentTokenType;

                if (tokenType1 != tokenType2)
                {
                    if (verboseWriter != null)
                    {
                        verboseWriter.WriteLine($"JSON token type mismatch at token number {tokenCount}");
                        verboseWriter.WriteLine($"  Token Type 1: {tokenType1}");
                        verboseWriter.WriteLine($"  Token Type 2: {tokenType2}");
                    }

                    return false;
                }

                switch (tokenType1)
                {
                    case JsonTokenType.NotStarted:
                    case JsonTokenType.BeginArray:
                    case JsonTokenType.EndArray:
                    case JsonTokenType.BeginObject:
                    case JsonTokenType.EndObject:
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                    case JsonTokenType.Null:
                        // No further comparison
                        break;

                    case JsonTokenType.FieldName:
                    case JsonTokenType.String:
                        {
                            string value1 = reader1.GetStringValue();
                            string value2 = reader2.GetStringValue();

                            if (value1 != value2)
                            {
                                if (verboseWriter != null)
                                {
                                    verboseWriter.WriteLine($"String value mismatch at token number {tokenCount}");
                                    verboseWriter.WriteLine($"  Value 1: {value1}");
                                    verboseWriter.WriteLine($"  Value 2: {value2}");
                                }

                                return false;
                            }
                        }
                        break;

                    case JsonTokenType.Number:
                        {
                            Number64 value1 = reader1.GetNumberValue();
                            Number64 value2 = reader2.GetNumberValue();

                            if (value1 != value2)
                            {
                                if (verboseWriter != null)
                                {
                                    verboseWriter.WriteLine($"Number value mismatch at token number {tokenCount}");
                                    verboseWriter.WriteLine($"  Value 1: {value1}");
                                    verboseWriter.WriteLine($"  Value 2: {value2}");
                                }

                                return false;
                            }
                        }
                        break;

                    case JsonTokenType.Int8:
                        {
                            sbyte value1 = reader1.GetInt8Value();
                            sbyte value2 = reader2.GetInt8Value();

                            if (value1 != value2)
                            {
                                if (verboseWriter != null)
                                {
                                    verboseWriter.WriteLine($"Int8 value mismatch at token number {tokenCount}");
                                    verboseWriter.WriteLine($"  Value 1: {value1}");
                                    verboseWriter.WriteLine($"  Value 2: {value2}");
                                }

                                return false;
                            }
                        }
                        break;

                    case JsonTokenType.Int16:
                        {
                            short value1 = reader1.GetInt16Value();
                            short value2 = reader2.GetInt16Value();

                            if (value1 != value2)
                            {
                                if (verboseWriter != null)
                                {
                                    verboseWriter.WriteLine($"Int16 value mismatch at token number {tokenCount}");
                                    verboseWriter.WriteLine($"  Value 1: {value1}");
                                    verboseWriter.WriteLine($"  Value 2: {value2}");
                                }

                                return false;
                            }
                        }
                        break;

                    case JsonTokenType.Int32:
                        {
                            int value1 = reader1.GetInt32Value();
                            int value2 = reader2.GetInt32Value();

                            if (value1 != value2)
                            {
                                if (verboseWriter != null)
                                {
                                    verboseWriter.WriteLine($"Int32 value mismatch at token number {tokenCount}");
                                    verboseWriter.WriteLine($"  Value 1: {value1}");
                                    verboseWriter.WriteLine($"  Value 2: {value2}");
                                }

                                return false;
                            }
                        }
                        break;

                    case JsonTokenType.Int64:
                        {
                            long value1 = reader1.GetInt64Value();
                            long value2 = reader2.GetInt64Value();

                            if (value1 != value2)
                            {
                                if (verboseWriter != null)
                                {
                                    verboseWriter.WriteLine($"Int64 value mismatch at token number {tokenCount}");
                                    verboseWriter.WriteLine($"  Value 1: {value1}");
                                    verboseWriter.WriteLine($"  Value 2: {value2}");
                                }

                                return false;
                            }
                        }
                        break;

                    case JsonTokenType.Float32:
                        {
                            float value1 = reader1.GetFloat32Value();
                            float value2 = reader2.GetFloat32Value();

                            if (value1 != value2)
                            {
                                if (verboseWriter != null)
                                {
                                    verboseWriter.WriteLine($"Float32 value mismatch at token number {tokenCount}");
                                    verboseWriter.WriteLine($"  Value 1: {value1}");
                                    verboseWriter.WriteLine($"  Value 2: {value2}");
                                }

                                return false;
                            }
                        }
                        break;

                    case JsonTokenType.Float64:
                        {
                            double value1 = reader1.GetFloat64Value();
                            double value2 = reader2.GetFloat64Value();

                            if (value1 != value2)
                            {
                                if (verboseWriter != null)
                                {
                                    verboseWriter.WriteLine($"Float64 value mismatch at token number {tokenCount}");
                                    verboseWriter.WriteLine($"  Value 1: {value1}");
                                    verboseWriter.WriteLine($"  Value 2: {value2}");
                                }

                                return false;
                            }
                        }
                        break;

                    case JsonTokenType.Guid:
                        {
                            Guid value1 = reader1.GetGuidValue();
                            Guid value2 = reader2.GetGuidValue();

                            if (value1 != value2)
                            {
                                if (verboseWriter != null)
                                {
                                    verboseWriter.WriteLine($"GUID value mismatch at token number {tokenCount}");
                                    verboseWriter.WriteLine($"  Value 1: {value1}");
                                    verboseWriter.WriteLine($"  Value 2: {value2}");
                                }

                                return false;
                            }
                        }
                        break;

                    case JsonTokenType.Binary:
                        {
                            ReadOnlyMemory<byte> value1 = reader1.GetBinaryValue();
                            ReadOnlyMemory<byte> value2 = reader2.GetBinaryValue();

                            if (value1.ToArray() != value2.ToArray())
                            {
                                if (verboseWriter != null)
                                {
                                    verboseWriter.WriteLine($"Binary value mismatch at token number {tokenCount}");
                                    verboseWriter.WriteLine($"  Value 1: {value1}");
                                    verboseWriter.WriteLine($"  Value 2: {value2}");
                                }

                                return false;
                            }
                        }
                        break;

                    default:
                        Assert.Fail($"Unexpected JsonTokenType value {tokenType1}.");
                        break;
                }
            }

            return true;
        }

        public static string[] SerializeResultBuffer(
            byte[] resultBuffer,
            JsonSerializationFormat serializationFormat)
        {
            if (resultBuffer == null) throw new ArgumentNullException(nameof(resultBuffer));

            const int TextLineSize = 100;
            const int RowSize = 16;

            string[] result;

            switch (serializationFormat)
            {
                case JsonSerializationFormat.Text:
                    {
                        string stringResult = Encoding.UTF8.GetString(resultBuffer);

                        result = new string[(stringResult.Length + TextLineSize - 1) / TextLineSize];
                        for (int i = 0; i < result.Length; i++)
                        {
                            int remainingLength = stringResult.Length - (i * TextLineSize);
                            result[i] = stringResult.Substring(i * TextLineSize, Math.Min(remainingLength, TextLineSize));
                        }
                    }
                    break;

                case JsonSerializationFormat.Binary:
                    {
                        List<string> lines = new List<string>();

                        StringBuilder stringBuilder = new StringBuilder();
                        for (int i = 0; i < resultBuffer.Length; i++)
                        {
                            if (i % RowSize == 0)
                            {
                                if (stringBuilder.Length > 0)
                                {
                                    lines.Add(stringBuilder.ToString());
                                    stringBuilder.Clear();
                                }

                                stringBuilder.Append(i.ToString("X8"));
                            }

                            if (i % (RowSize / 2) == 0)
                            {
                                stringBuilder.Append(' ');
                            }

                            stringBuilder.Append(' ').Append(resultBuffer[i].ToString("X2"));
                        }

                        if (stringBuilder.Length > 0)
                        {
                            lines.Add(stringBuilder.ToString());
                        }

                        result = lines.ToArray();
                    }
                    break;

                default:
                    Assert.Fail($"Unexpected JsonSerializationFormat: {serializationFormat}.");
                    result = null;
                    break;
            }

            return result;
        }

        public enum RewriteScenario
        {
            NavigatorRoot,
            NavigatorNode,
            ReaderAll,
            ReaderToken,
        };

        public class SerializationSpec
        {
            private SerializationSpec(JsonSerializationFormat serializationFormat, JsonWriteOptions writeOptions, bool isNewtonsoft)
            {
                this.SerializationFormat = serializationFormat;
                this.WriteOptions = writeOptions;
                this.IsNewtonsoft = isNewtonsoft;
            }

            public static SerializationSpec Text(JsonWriteOptions writeOptions = JsonWriteOptions.None)
            {
                return new SerializationSpec(JsonSerializationFormat.Text, writeOptions, false);
            }

            public static SerializationSpec Binary(JsonWriteOptions writeOptions = JsonWriteOptions.None)
            {
                return new SerializationSpec(JsonSerializationFormat.Binary, writeOptions, false);
            }

            public static SerializationSpec Newtonsoft()
            {
                return new SerializationSpec(JsonSerializationFormat.Text, JsonWriteOptions.None, true);
            }

            public string SerializationFormatToString()
            {
                return this.IsNewtonsoft ? "NewtonsoftText" : this.SerializationFormat.ToString();
            }

            public JsonSerializationFormat SerializationFormat { get; }
            public JsonWriteOptions WriteOptions { get; }
            public bool IsNewtonsoft { get; }
        }

        public class RoundTripResult
        {
            public RoundTripResult(ReadOnlyMemory<byte> outputResult, long executionTime, long verificationTime)
            {
                this.OutputResult = outputResult;
                this.ExecutionTime = executionTime;
                this.VerificationTime = verificationTime;
            }

            public ReadOnlyMemory<byte> OutputResult { get; }
            public long ExecutionTime { get; }
            public long VerificationTime { get; }
        }
    }
}