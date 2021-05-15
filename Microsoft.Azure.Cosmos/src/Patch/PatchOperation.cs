//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using Newtonsoft.Json;

    /// <summary>
    /// Details of Patch operation that is to be applied to the referred Cosmos item.
    /// </summary>
#if PREVIEW
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

        /// <summary>
        /// Serializes the value parameter, if specified for the PatchOperation.
        /// </summary>
        /// <param name="cosmosSerializer">Serializer to be used.</param>
        /// <param name="valueParam">Outputs the serialized stream if value parameter is specified, null otherwise.</param>
        /// <returns>True if value is serialized, false otherwise.</returns>
        /// <remarks>Output stream should be disposed after use.</remarks>
        public virtual bool TrySerializeValueParameter(
            CosmosSerializer cosmosSerializer,
            out Stream valueParam)
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
        public static PatchOperation Add<T>(
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
        public static PatchOperation Remove(string path)
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
        public static PatchOperation Replace<T>(
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
        public static PatchOperation Set<T>(
            string path,
            T value)
        {
            return new PatchOperationCore<T>(
                PatchOperationType.Set,
                path,
                value);
        }

        /// <summary>
        /// Create <see cref="PatchOperation"/> to Increment a value.
        /// </summary>
        /// <param name="path">Target location reference.</param>
        /// <param name="value">The value to be Incremented by at the specified path.</param>
        /// <returns>PatchOperation instance for specified input.</returns>
        public static PatchOperation Increment(
            string path,
            long value)
        {
            return new PatchOperationCore<long>(
                PatchOperationType.Increment,
                path,
                value);
        }

        /// <summary>
        /// Create <see cref="PatchOperation"/> to Increment a value.
        /// </summary>
        /// <param name="path">Target location reference.</param>
        /// <param name="value">The value to be Incremented by at the specified path.</param>
        /// <returns>PatchOperation instance for specified input.</returns>
        public static PatchOperation Increment(
            string path,
            double value)
        {
            return new PatchOperationCore<double>(
                PatchOperationType.Increment,
                path,
                value);
        }
    }
}
