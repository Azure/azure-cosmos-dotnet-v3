//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Newtonsoft.Json;

#if PREVIEW
    public
#else
    internal
#endif
        sealed class PatchSpecification
    {
        private const int maxOperationsAllowed = 100;

        [JsonProperty(PropertyName = "operations")]
        internal List<PatchOperation> Operations { get; }

        public PatchSpecification()
        {
            this.Operations = new List<PatchOperation>();
        }

        public PatchSpecification Add<T>(
            string path,
            T value)
        {
            this.ValidateNumberOfOperations();
            this.ValidatePathArgument(path);
            this.ValidateValueArgument(value);

            this.Operations.Add(
                new PatchOperation<T>(
                    PatchOperationType.Add,
                    path,
                    value));

            return this;
        }

        public PatchSpecification Remove(string path)
        {
            this.ValidateNumberOfOperations();
            this.ValidatePathArgument(path);

            this.Operations.Add(
                new PatchOperation(
                    PatchOperationType.Remove,
                    path));

            return this;
        }

        public PatchSpecification Replace<T>(
            string path,
            T value)
        {
            this.ValidateNumberOfOperations();
            this.ValidatePathArgument(path);
            this.ValidateValueArgument(value);

            this.Operations.Add(
                new PatchOperation<T>(
                    PatchOperationType.Replace,
                    path,
                    value));

            return this;
        }

        public PatchSpecification Set<T>(
            string path,
            T value)
        {
            this.ValidateNumberOfOperations();
            this.ValidatePathArgument(path);
            this.ValidateValueArgument(value);

            this.Operations.Add(
                new PatchOperation<T>(
                    PatchOperationType.Set,
                    path,
                    value));

            return this;
        }

        private void ValidatePathArgument(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }
        }

        private void ValidateValueArgument(object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
        }

        private void ValidateNumberOfOperations()
        {
            if (this.Operations.Count == maxOperationsAllowed)
            {
                throw CosmosExceptionFactory.CreateBadRequestException(
                    $"Maximum number of operations allowed per {nameof(PatchSpecification)} is {maxOperationsAllowed}.");
            }
        }
    }
}
