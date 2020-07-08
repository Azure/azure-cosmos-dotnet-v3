//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

#if INTERNAL
    public
#else
    internal
#endif
        sealed class PatchSpecification
    {
        [JsonProperty(PropertyName = "operations")]
        internal List<PatchOperation> Operations { get; }

        public PatchSpecification()
        {
            this.Operations = new List<PatchOperation>();
        }

        /// <summary>
        /// Adds an operation to add a value.
        /// </summary>
        /// <typeparam name="T">Type of <paramref name="value"/></typeparam>
        /// <param name="path">Target location reference.</param>
        /// <param name="value">The value to be added.</param>
        /// <returns>The patch specification instance with the operation added.</returns>
        public PatchSpecification Add<T>(
            string path,
            T value)
        {
            this.Operations.Add(
                new AddPatchOperation<T>(
                    path,
                    value));

            return this;
        }

        /// <summary>
        /// Adds an operation to remove a value.
        /// </summary>
        /// <param name="path">Target location reference.</param>
        /// <returns>The patch specification instance with the operation added.</returns>
        public PatchSpecification Remove(string path)
        {
            this.Operations.Add(
                new RemovePatchOperation(
                    path));

            return this;
        }

        /// <summary>
        /// Adds an operation to replace a value.
        /// </summary>
        /// <typeparam name="T">Type of <paramref name="value"/></typeparam>
        /// <param name="path">Target location reference.</param>
        /// <param name="value">The new value.</param>
        /// <returns>The patch specification instance with the operation added.</returns>
        public PatchSpecification Replace<T>(
            string path,
            T value)
        {
            this.Operations.Add(
                new ReplacePatchOperation<T>(
                    path,
                    value));

            return this;
        }

        /// <summary>
        /// Adds an operation to set a value.
        /// </summary>
        /// <typeparam name="T">Type of <paramref name="value"/></typeparam>
        /// <param name="path">Target location reference.</param>
        /// <param name="value">The value to be set at the specified path.</param>
        /// <returns>The patch specification instance with the operation added.</returns>
        public PatchSpecification Set<T>(
            string path,
            T value)
        {
            this.Operations.Add(
                new SetPatchOperation<T>(
                    path,
                    value));

            return this;
        }
    }
}
