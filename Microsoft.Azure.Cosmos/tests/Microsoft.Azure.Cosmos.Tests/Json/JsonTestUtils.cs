namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;

    internal static class JsonTestUtils
    {
        public static byte[] ConvertTextToBinary(string text, JsonStringDictionary jsonStringDictionary = null)
        {
            IJsonWriter binaryWriter = JsonWriter.Create(JsonSerializationFormat.Binary, jsonStringDictionary);
            IJsonReader textReader = JsonReader.Create(Encoding.UTF8.GetBytes(text));
            binaryWriter.WriteAll(textReader);
            return binaryWriter.GetResult().ToArray();
        }

        public static string ConvertBinaryToText(ReadOnlyMemory<byte> binary, JsonStringDictionary jsonStringDictionary = null)
        {
            IJsonReader binaryReader = JsonReader.Create(binary, jsonStringDictionary);
            IJsonWriter textWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            textWriter.WriteAll(binaryReader);
            return Encoding.UTF8.GetString(textWriter.GetResult().ToArray());
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
    }
}
