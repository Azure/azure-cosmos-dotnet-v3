//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal sealed class CosmosSerializationFormatOptions
    {
        public delegate IJsonNavigator CreateCustomNavigator(ReadOnlyMemory<byte> content);

        public delegate IJsonWriter CreateCustomWriter();

        /// <summary>
        /// What serialization format to request the response in from the backend
        /// </summary>
        public string ContentSerializationFormat { get; }

        /// <summary>
        /// Request multiple serialization formats, backend will decide which formats is best and choose appropriate format.
        /// </summary>
        public string SupportedSerializationFormats { get; }

        /// <summary>
        /// Creates a navigator that can navigate a JSON in the specified ContentSerializationFormat
        /// </summary>
        public CreateCustomNavigator CreateCustomNavigatorCallback { get; }

        /// <summary>
        /// Creates a writer to use to write out the stream.
        /// </summary>
        public CreateCustomWriter CreateCustomWriterCallback { get; }

        public CosmosSerializationFormatOptions(
            string contentSerializationFormat,
            string supportedSerializationFormats,
            CreateCustomNavigator createCustomNavigator,
            CreateCustomWriter createCustomWriter)
        {
            if (contentSerializationFormat == null)
            {
                throw new ArgumentNullException(nameof(contentSerializationFormat));
            }

            if (supportedSerializationFormats == null)
            {
                throw new ArgumentNullException(nameof(supportedSerializationFormats));
            }

            if (createCustomNavigator == null)
            {
                throw new ArgumentNullException(nameof(createCustomNavigator));
            }

            if (createCustomWriter == null)
            {
                throw new ArgumentNullException(nameof(createCustomWriter));
            }

            this.ContentSerializationFormat = contentSerializationFormat;
            this.SupportedSerializationFormats = supportedSerializationFormats.Length == 0 ? "JsonText, CosmosBinary" : supportedSerializationFormats;
            this.CreateCustomNavigatorCallback = createCustomNavigator;
            this.CreateCustomWriterCallback = createCustomWriter;
        }
    }
}
