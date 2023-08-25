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
        /// Creates a navigator that can navigate a JSON in the specified ContentSerializationFormat
        /// </summary>
        public CreateCustomNavigator CreateCustomNavigatorCallback { get; }

        /// <summary>
        /// Creates a writer to use to write out the stream.
        /// </summary>
        public CreateCustomWriter CreateCustomWriterCallback { get; }

        public CosmosSerializationFormatOptions(
            string contentSerializationFormat,
            CreateCustomNavigator createCustomNavigator,
            CreateCustomWriter createCustomWriter)
        {
            if (contentSerializationFormat == null)
            {
                throw new ArgumentNullException(nameof(contentSerializationFormat));
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
            this.CreateCustomNavigatorCallback = createCustomNavigator;
            this.CreateCustomWriterCallback = createCustomWriter;
        }
    }
}
