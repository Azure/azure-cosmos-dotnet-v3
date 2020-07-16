//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Details of Patch operation that is to be applied to the referred Cosmos item.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
        class PatchOperation
    {
        /// <summary>
        /// Patch operation type.
        /// </summary>
        [JsonProperty(PropertyName = PatchConstants.PropertyNames.OperationType)]
        [JsonConverter(typeof(StringEnumConverter))]
        public PatchOperationType OperationType { get; }

        /// <summary>
        /// Target location reference. 
        /// </summary>
        [JsonProperty(PropertyName = PatchConstants.PropertyNames.Path)]
        public string Path { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PatchOperation"/> class.
        /// </summary>
        /// <param name="operationType">Specifies the type of Patch operation.</param>
        /// <param name="path">Specifies the path to target location.</param>
        internal PatchOperation(
            PatchOperationType operationType,
            string path)
        {
            this.OperationType = operationType;
            this.Path = string.IsNullOrWhiteSpace(path)
                ? throw new ArgumentNullException(nameof(path))
                : path;
        }

        internal virtual bool TrySerializeValueParameter(
            CosmosSerializer cosmosSerializer,
            out string valueParam)
        {
            valueParam = null;
            return false;
        }

        /// <summary>
        /// Create <see cref="PatchOperation{T}"/> to add a value.
        /// </summary>
        /// <typeparam name="T">Type of <paramref name="value"/></typeparam>
        /// <param name="path">Target location reference.</param>
        /// <param name="value">The value to be added.</param>
        /// <returns>PatchOperation instance for specified input.</returns>
        public static PatchOperation CreateAddOperation<T>(
            string path,
            T value)
        {
            return new PatchOperation<T>(
                PatchOperationType.Add,
                path,
                value);
        }

        /// <summary>
        /// Create <see cref="PatchOperation"/> to remove a value.
        /// </summary>
        /// <param name="path">Target location reference.</param>
        /// <returns>PatchOperation instance for specified input.</returns>
        public static PatchOperation CreateRemoveOperation(string path)
        {
            return new PatchOperation(
                PatchOperationType.Remove,
                path);
        }

        /// <summary>
        /// Create <see cref="PatchOperation{T}"/> to replace a value.
        /// </summary>
        /// <typeparam name="T">Type of <paramref name="value"/></typeparam>
        /// <param name="path">Target location reference.</param>
        /// <param name="value">The new value.</param>
        /// <returns>PatchOperation instance for specified input.</returns>
        public static PatchOperation CreateReplaceOperation<T>(
            string path,
            T value)
        {
            return new PatchOperation<T>(
                PatchOperationType.Replace,
                path,
                value);
        }

        /// <summary>
        /// Create <see cref="PatchOperation{T}"/> to set a value.
        /// </summary>
        /// <typeparam name="T">Type of <paramref name="value"/></typeparam>
        /// <param name="path">Target location reference.</param>
        /// <param name="value">The value to be set at the specified path.</param>
        /// <returns>PatchOperation instance for specified input.</returns>
        public static PatchOperation CreateSetOperation<T>(
            string path,
            T value)
        {
            return new PatchOperation<T>(
                PatchOperationType.Set,
                path,
                value);
        }
    }
}
