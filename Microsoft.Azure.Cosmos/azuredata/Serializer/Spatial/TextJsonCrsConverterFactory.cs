//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Cosmos.Spatial;

    internal sealed class TextJsonCrsConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(Crs)
                || typeToConvert == typeof(NamedCrs)
                || typeToConvert == typeof(LinkedCrs)
                || typeToConvert == typeof(UnspecifiedCrs);
        }

        public override JsonConverter CreateConverter(
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            return new TextJsonCrsConverter();
        }
    }
}
