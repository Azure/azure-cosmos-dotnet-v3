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
    public abstract class PatchOperation
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
        /// Source location reference (used in case of move)
        /// </summary>
        [JsonProperty(PropertyName = PatchConstants.PropertyNames.From)]
        public virtual string From { get; set; } = null;

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
        /// <remarks>
        /// <para>The Replace operation is similar to Set in that it updates a field's value, but differs in one crucial way: Replace
        /// only updates an existing field. If the field is absent, Replace will fail and throw a **400 BadRequest** (HTTP status code 400).
        /// In contrast, Set will either update the existing field or create a new one if it does not exist. Users should be aware that
        /// Replace follows strict semantics and will not add new fields, unlike Set, which is more flexible.</para>
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// Example 1: Replace Operation on a Non-existent Property
        ///
        /// ToDoActivity toDoActivity = await this.container.ReadItemAsync<ToDoActivity>("id", new PartitionKey("partitionKey"));
        ///
        /// /* toDoActivity = {
        ///     "id" : "someId",
        ///     "status" : "someStatusPK",
        ///     "description" : "someDescription",
        ///     "frequency" : 7
        /// }*/
        ///
        /// This example illustrates what happens when a Replace operation is attempted on a property that does not exist.
        /// In this case, trying to replace the "priority" property, which is not present in the original ToDoActivity item, results in an HTTP 400 BadRequest.
        ///
        /// List<PatchOperation> patchOperations = new List<PatchOperation>()
        /// {
        ///     PatchOperation.Replace("/priority", "High")
        /// };
        ///
        /// try
        /// {
        ///     ItemResponse<ToDoActivity> item = await this.container.PatchItemAsync<ToDoActivity>(toDoActivity.id, new PartitionKey(toDoActivity.status), patchOperations);
        /// }
        /// catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        /// {
        ///     ...
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso href="https://learn.microsoft.com/azure/cosmos-db/partial-document-update#supported-modes">Supported partial document update modes</seealso>
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

        /// <summary>
        /// Create <see cref="PatchOperation"/> to move an object/value.
        /// </summary>
        /// <param name="from">The source location of the object/value.</param>
        /// <param name="path">Target location reference.</param>
        /// <returns>PatchOperation instance for specified input.</returns>
        public static PatchOperation Move(
            string from,
            string path)
        {
            return new PatchOperationCore<string>(
                PatchOperationType.Move,
                path,
                from);
        }
    }
}
