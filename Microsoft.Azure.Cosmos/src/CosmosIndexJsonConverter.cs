//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class CosmosIndexJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(Index).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (objectType != typeof(Index))
            {
                return null;
            }

            JToken indexToken = JToken.Load(reader);

            if (indexToken.Type == JTokenType.Null)
            {
                return null;
            }

            if (indexToken.Type != JTokenType.Object)
            {
                throw new JsonSerializationException(
                    string.Format(CultureInfo.CurrentCulture, Documents.RMResources.InvalidIndexSpecFormat));
            }

            JToken indexKindToken = indexToken[Documents.Constants.Properties.IndexKind];
            if (indexKindToken == null || indexKindToken.Type != JTokenType.String)
            {
                throw new JsonSerializationException(
                    string.Format(CultureInfo.CurrentCulture, Documents.RMResources.InvalidIndexSpecFormat));
            }

            if (Enum.TryParse(indexKindToken.Value<string>(), out IndexKind indexKind))
            {
                object index = indexKind switch
                {
                    IndexKind.Hash => new HashIndex(),
                    IndexKind.Range => new RangeIndex(),
                    IndexKind.Spatial => new SpatialIndex(),
                    _ => throw new JsonSerializationException(
                        string.Format(CultureInfo.CurrentCulture, Documents.RMResources.InvalidIndexKindValue, indexKind)),
                };
                serializer.Populate(indexToken.CreateReader(), index);
                return index;
            }
            else
            {
                throw new JsonSerializationException(
                    string.Format(CultureInfo.CurrentCulture, Documents.RMResources.InvalidIndexKindValue, indexKindToken.Value<string>()));
            }
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}