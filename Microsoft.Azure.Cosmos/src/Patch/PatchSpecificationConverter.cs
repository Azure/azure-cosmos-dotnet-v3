//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Patch
{
    using System;
    using Newtonsoft.Json;

    internal sealed class PatchSpecificationConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => true;

        public override bool CanRead => false;

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override void WriteJson(
            JsonWriter writer,
            object value,
            JsonSerializer serializer)
        {
            try
            {
                PatchSpecification patchSpecification = (PatchSpecification)value;
                {
                    serializer.Serialize(writer, patchSpecification.operations);
                }
            }
            catch (Exception ex)
            {
                throw new JsonSerializationException(string.Empty, ex);
            }
        }
    }
}
