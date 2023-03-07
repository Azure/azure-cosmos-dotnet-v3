//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal sealed class CosmosSerializationFormatOptions
    {
        public delegate IJsonWriter CreateCustomWriter();

        /// <summary>
        /// Creates a writer to use to write out the stream.
        /// </summary>
        public CreateCustomWriter CreateCustomWriterCallback { get; }

        public CosmosSerializationFormatOptions(
            CreateCustomWriter createCustomWriter)
        {
            if (createCustomWriter == null)
            {
                throw new ArgumentNullException(nameof(createCustomWriter));
            }

            this.CreateCustomWriterCallback = createCustomWriter;
        }
    }
}
