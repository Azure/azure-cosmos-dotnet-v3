//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;

    internal sealed class PatchOperationCore<T> : PatchOperation<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PatchOperationCore{T}"/> class.
        /// </summary>
        /// <param name="operationType">Specifies the type of Patch operation.</param>
        /// <param name="path">Specifies the path to target location.</param>
        /// <param name="value">Specifies the value to be used.</param>
        public PatchOperationCore(
            PatchOperationType operationType,
            string path,
            T value)
        {
            this.OperationType = operationType;
            this.Path = string.IsNullOrWhiteSpace(path)
                ? throw new ArgumentNullException(nameof(path))
                : path;
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public override T Value { get; }

        public override PatchOperationType OperationType { get; }

        public override string Path { get; }

        public override bool TrySerializeValueParameter(
            CosmosSerializer cosmosSerializer,
            out Stream valueParam)
        {
            // If value is of type Stream, do not serialize
            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                valueParam = (Stream)(object)this.Value;
            }
            else
            {
                // Use the user serializer so custom conversions are correctly handled
                valueParam = cosmosSerializer.ToStream(this.Value);
            }

            return true;
        }
    }
}