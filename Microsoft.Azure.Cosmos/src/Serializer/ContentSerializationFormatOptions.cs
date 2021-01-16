// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Serializer
{
    using System;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Documents;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
        abstract class ContentSerializationFormatOptions
    {
        public delegate IJsonNavigator CreateNavigator(ReadOnlyMemory<byte> content);

        protected ContentSerializationFormatOptions(
            ContentSerializationFormat contentSerializationFormat)
        {
            this.ContentSerializationFormat = contentSerializationFormat;
        }

        public ContentSerializationFormat ContentSerializationFormat { get; }

        public ContentSerializationFormatOptions Create(
            ContentSerializationFormat contentSerializationFormat)
        {
            return new NativelySupportedJsonSerializationFormatOptions(contentSerializationFormat);
        }

        public ContentSerializationFormatOptions Create(
            ContentSerializationFormat contentSerializationFormat,
            CreateNavigator createNavigator)
        {
            return new CustomJsonSerializationFormatOptions(
                contentSerializationFormat,
                createNavigator);
        }

        public sealed class NativelySupportedJsonSerializationFormatOptions : ContentSerializationFormatOptions
        {
            public NativelySupportedJsonSerializationFormatOptions(
                ContentSerializationFormat contentSerializationFormat)
                : base(contentSerializationFormat)
            {
            }
        }

        public sealed class CustomJsonSerializationFormatOptions : ContentSerializationFormatOptions
        {
            public CustomJsonSerializationFormatOptions(
                ContentSerializationFormat contentSerializationFormat,
                CreateNavigator createNavigator)
                : base(contentSerializationFormat)
            {
                this.createNavigator = createNavigator ?? throw new ArgumentNullException(nameof(contentSerializationFormat));
            }

            public CreateNavigator createNavigator { get; }
        }
    }
}
