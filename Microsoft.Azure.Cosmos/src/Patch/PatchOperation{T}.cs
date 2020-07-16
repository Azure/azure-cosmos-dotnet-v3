//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using Newtonsoft.Json;

    /// <summary>
    /// Defines PatchOperation with a value parameter.
    /// </summary>
    /// <typeparam name="T">Data type of value provided for PatchOperation.</typeparam>
#if INTERNAL
    public
#else
    internal
#endif
        class PatchOperation<T> : PatchOperation
    {
        /// <summary>
        /// Value parameter.
        /// </summary>
        [JsonProperty(PropertyName = PatchConstants.PropertyNames.Value)]
        public T Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PatchOperation{T}"/> class.
        /// </summary>
        /// <param name="operationType">Specifies the type of Patch operation.</param>
        /// <param name="path">Specifies the path to target location.</param>
        /// <param name="value">Specifies the value to be used.</param>
        internal PatchOperation(
            PatchOperationType operationType,
            string path,
            T value)
            : base(operationType, path)
        {
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        internal override bool TrySerializeValueParameter(
            CosmosSerializer cosmosSerializer,
            out string valueParam)
        {
            // Use the user serializer so custom conversions are correctly handled
            using (Stream stream = cosmosSerializer.ToStream(this.Value))
            {
                using (StreamReader streamReader = new StreamReader(stream))
                {
                    valueParam = streamReader.ReadToEnd();
                }
            }

            return true;
        }
    }
}
