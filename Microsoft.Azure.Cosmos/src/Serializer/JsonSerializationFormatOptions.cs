// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Serializer
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
        abstract class JsonSerializationFormatOptions
    {
        public delegate IJsonNavigator CreateNavigator(ReadOnlyMemory<byte> content);

        protected JsonSerializationFormatOptions(
            JsonSerializationFormat jsonSerializationFormat)
        {
            this.JsonSerializationFormat = jsonSerializationFormat;
        }

        public JsonSerializationFormat JsonSerializationFormat { get; }

        public static JsonSerializationFormatOptions Create(
            JsonSerializationFormat jsonSerializationFormat)
        {
            return new NativelySupportedJsonSerializationFormatOptions(jsonSerializationFormat);
        }

        public static JsonSerializationFormatOptions Create(
            JsonSerializationFormat jsonSerializationFormat,
            CreateNavigator createNavigator)
        {
            return new CustomJsonSerializationFormatOptions(
                jsonSerializationFormat,
                createNavigator);
        }

        public sealed class NativelySupportedJsonSerializationFormatOptions : JsonSerializationFormatOptions
        {
            public NativelySupportedJsonSerializationFormatOptions(
                JsonSerializationFormat jsonSerializationFormat)
                : base(jsonSerializationFormat)
            {
            }
        }

        public sealed class CustomJsonSerializationFormatOptions : JsonSerializationFormatOptions
        {
            public CustomJsonSerializationFormatOptions(
                JsonSerializationFormat jsonSerializationFormat,
                CreateNavigator createNavigator)
                : base(jsonSerializationFormat)
            {
                this.createNavigator = createNavigator ?? throw new ArgumentNullException(nameof(jsonSerializationFormat));
            }

            public CreateNavigator createNavigator { get; }
        }
    }
}
