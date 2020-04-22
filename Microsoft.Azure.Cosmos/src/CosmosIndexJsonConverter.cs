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

            IndexKind indexKind;
            if (Enum.TryParse(indexKindToken.Value<string>(), out indexKind))
            {
                object index;
                switch (indexKind)
                {
                    case IndexKind.Hash:
                        index = new HashIndex();
                        break;
                    case IndexKind.Range:
                        index = new RangeIndex();
                        break;
                    case IndexKind.Spatial:
                        index = new SpatialIndex();
                        break;
                    default:
                        throw new JsonSerializationException(
                            string.Format(CultureInfo.CurrentCulture, Documents.RMResources.InvalidIndexKindValue, indexKind));
                }

                serializer.Populate(indexToken.CreateReader(), index);
                return index;
            }
            else
            {
                throw new JsonSerializationException(
                    string.Format(CultureInfo.CurrentCulture, Documents.RMResources.InvalidIndexKindValue, indexKindToken.Value<string>()));
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}