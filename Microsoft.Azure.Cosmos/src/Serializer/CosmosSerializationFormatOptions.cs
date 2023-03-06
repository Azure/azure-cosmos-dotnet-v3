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
        /// Creates a navigator that can navigate a JSON in the specified SerializationFormat
        /// </summary>
        public CreateCustomNavigator CreateCustomNavigatorCallback { get; }

        /// <summary>
        /// Creates a writer to use to write out the stream.
        /// </summary>
        public CreateCustomWriter CreateCustomWriterCallback { get; }

        public CosmosSerializationFormatOptions(
            CreateCustomNavigator createCustomNavigator,
            CreateCustomWriter createCustomWriter)
        {
            if (createCustomNavigator == null)
            {
                throw new ArgumentNullException(nameof(createCustomNavigator));
            }

            if (createCustomWriter == null)
            {
                throw new ArgumentNullException(nameof(createCustomWriter));
            }

            this.CreateCustomNavigatorCallback = createCustomNavigator;
            this.CreateCustomWriterCallback = createCustomWriter;
        }
    }
}
