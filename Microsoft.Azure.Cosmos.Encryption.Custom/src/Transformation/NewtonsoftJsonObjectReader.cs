//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.IO;
    using System.Text;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal static class NewtonsoftJsonObjectReader
    {
        internal const string InvalidBodyMessage = "The response body must contain a JSON object.";

        public static JObject Read(Stream input)
        {
            StreamPositionHelper.ResetToStart(input, nameof(input));

            try
            {
                using StreamReader streamReader = new (
                    input,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 1024,
                    leaveOpen: true);
                using JsonTextReader jsonReader = new (streamReader)
                {
                    ArrayPool = JsonArrayPool.Instance,
                    DateParseHandling = DateParseHandling.None,
                    MaxDepth = 64,
                };

                JToken token = JsonSerializer.CreateDefault().Deserialize<JToken>(jsonReader);
                if (token is not JObject document)
                {
                    throw new InvalidOperationException(InvalidBodyMessage);
                }

                while (jsonReader.Read())
                {
                    if (jsonReader.TokenType != JsonToken.Comment)
                    {
                        throw new InvalidOperationException(InvalidBodyMessage);
                    }
                }

                StreamPositionHelper.ResetToStart(input, nameof(input));
                return document;
            }
            catch (JsonException exception)
            {
                StreamPositionHelper.TryResetToStart(input);
                throw new InvalidOperationException(InvalidBodyMessage, exception);
            }
            catch
            {
                StreamPositionHelper.TryResetToStart(input);
                throw;
            }
        }
    }
}
