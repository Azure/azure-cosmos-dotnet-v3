namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;

    internal static class JsonTestUtils
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
            string json,
            int? seed = null,
            int maxNumberOfItems = 10)
        {
            Newtonsoft.Json.Linq.JToken root = (Newtonsoft.Json.Linq.JToken)Newtonsoft.Json.JsonConvert.DeserializeObject(json);
            Random random = seed.HasValue ? new Random(seed.Value) : new Random();

            string sampledJson;
            if(root.Type == Newtonsoft.Json.Linq.JTokenType.Array)
            {
                Newtonsoft.Json.Linq.JArray array = (Newtonsoft.Json.Linq.JArray)root;
                IEnumerable<Newtonsoft.Json.Linq.JToken> tokens = array
                    .OrderBy(x => random.Next())
                    .Take(random.Next(maxNumberOfItems));
                Newtonsoft.Json.Linq.JArray newArray = new Newtonsoft.Json.Linq.JArray();
                foreach (Newtonsoft.Json.Linq.JToken token in tokens)
                {
                    newArray.Add(token);
                }

                sampledJson = newArray.ToString();
            }
            else if(root.Type == Newtonsoft.Json.Linq.JTokenType.Object)
            {
                Newtonsoft.Json.Linq.JObject jobject = (Newtonsoft.Json.Linq.JObject)root;
                IEnumerable<Newtonsoft.Json.Linq.JProperty> properties = jobject
                    .Properties()
                    .OrderBy(x => random.Next())
                    .Take(maxNumberOfItems);
                Newtonsoft.Json.Linq.JObject newObject = new Newtonsoft.Json.Linq.JObject();
                foreach(Newtonsoft.Json.Linq.JProperty property in properties)
                {
                    newObject.Add(property);
                }

                sampledJson = newObject.ToString();
            }
            else
            {
                sampledJson = json;
            }

            return sampledJson;
        }

        public static JsonToken[] ReadJsonDocument(string json)
        {
            IJsonReader reader = JsonReader.Create(Encoding.UTF8.GetBytes(json));
            return ReadJsonDocument(reader);
        }

        public static JsonToken[] ReadJsonDocument(IJsonReader reader)
        {
            List<JsonToken> tokens = new List<JsonToken>();
            while (reader.Read())
            {
                JsonToken token;
                switch (reader.CurrentTokenType)
                {
                    case JsonTokenType.NotStarted:
                        throw new InvalidOperationException();

                    case JsonTokenType.BeginArray:
                        token = JsonToken.ArrayStart();
                        break;

                    case JsonTokenType.EndArray:
                        token = JsonToken.ArrayEnd();
                        break;

                    case JsonTokenType.BeginObject:
                        token = JsonToken.ObjectStart();
                        break;

                    case JsonTokenType.EndObject:
                        token = JsonToken.ObjectEnd();
                        break;

                    case JsonTokenType.String:
                        token = JsonToken.String(reader.GetStringValue());
                        break;

                    case JsonTokenType.Number:
                        token = JsonToken.Number(reader.GetNumberValue());
                        break;

                    case JsonTokenType.True:
                        token = JsonToken.Boolean(true);
                        break;

                    case JsonTokenType.False:
                        token = JsonToken.Boolean(false);
                        break;

                    case JsonTokenType.Null:
                        token = JsonToken.Null();
                        break;

                    case JsonTokenType.FieldName:
                        token = JsonToken.FieldName(reader.GetStringValue());
                        break;

                    case JsonTokenType.Int8:
                        token = JsonToken.Int8(reader.GetInt8Value());
                        break;

                    case JsonTokenType.Int16:
                        token = JsonToken.Int16(reader.GetInt16Value());
                        break;

                    case JsonTokenType.Int32:
                        token = JsonToken.Int32(reader.GetInt32Value());
                        break;

                    case JsonTokenType.Int64:
                        token = JsonToken.Int64(reader.GetInt64Value());
                        break;

                    case JsonTokenType.UInt32:
                        token = JsonToken.UInt32(reader.GetUInt32Value());
                        break;

                    case JsonTokenType.Float32:
                        token = JsonToken.Float32(reader.GetFloat32Value());
                        break;

                    case JsonTokenType.Float64:
                        token = JsonToken.Float64(reader.GetFloat64Value());
                        break;

                    case JsonTokenType.Guid:
                        token = JsonToken.Guid(reader.GetGuidValue());
                        break;

                    case JsonTokenType.Binary:
                        token = JsonToken.Binary(reader.GetBinaryValue());
                        break;

                    default:
                        throw new ArgumentException($"Unknown {nameof(JsonTokenType)}: {reader.CurrentTokenType}");
                }

                tokens.Add(token);
            }

            return tokens.ToArray();
        }
    }
}
