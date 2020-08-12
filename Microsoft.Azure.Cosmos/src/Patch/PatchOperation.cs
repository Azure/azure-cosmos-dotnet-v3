//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

    /// <summary>
    /// Details of Patch operation that is to be applied to the referred Cosmos item.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
        abstract class PatchOperation
    {
        /// <summary>
        /// Patch operation type.
        /// </summary>
        [JsonProperty(PropertyName = PatchConstants.PropertyNames.OperationType)]
        public abstract PatchOperationType OperationType { get; }

        /// <summary>
        /// Target location reference. 
        /// </summary>
        [JsonProperty(PropertyName = PatchConstants.PropertyNames.Path)]
        public abstract string Path { get; }

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
            return new PatchOperationCore<T>(
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
            return new PatchOperationCore(
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
            return new PatchOperationCore<T>(
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
            return new PatchOperationCore<T>(
                PatchOperationType.Set,
                path,
                value);
        }
    }
}
