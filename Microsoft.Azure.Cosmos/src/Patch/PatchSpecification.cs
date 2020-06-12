//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Patch
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Newtonsoft.Json;

    [JsonConverter(typeof(PatchSpecificationConverter))]
#if PREVIEW
    public
#else
    internal
#endif
        sealed class PatchSpecification
    {
        private const int maxOperationsAllowed = 100;

        public List<PatchOperation> operations { get; private set; }

        public PatchSpecification()
        {
            this.operations = new List<PatchOperation>();
        }

        public PatchSpecification Add(
            string path,
            object value)
        {
            this.ValidateNumberOfOperations();
            this.ValidatePathArguement(path);
            this.ValidateValueArguement(value);

            this.operations.Add(
                new PatchOperation(
                    PatchOperationType.add,
                    path,
                    value));

            return this;
        }

        public PatchSpecification Remove(string path)
        {
            this.ValidateNumberOfOperations();
            this.ValidatePathArguement(path);

            this.operations.Add(
                new PatchOperation(
                    PatchOperationType.remove,
                    path));

            return this;
        }

        public PatchSpecification Replace(
            string path,
            object value)
        {
            this.ValidateNumberOfOperations();
            this.ValidatePathArguement(path);
            this.ValidateValueArguement(value);

            this.operations.Add(
                new PatchOperation(
                    PatchOperationType.replace,
                    path,
                    value));

            return this;
        }

        public PatchSpecification Set(
            string path,
            object value)
        {
            this.ValidateNumberOfOperations();
            this.ValidatePathArguement(path);
            this.ValidateValueArguement(value);

            this.operations.Add(
                new PatchOperation(
                    PatchOperationType.set,
                    path,
                    value));

            return this;
        }

        private void ValidatePathArguement(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }
        }

        private void ValidateValueArguement(object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
        }

        private void ValidateNumberOfOperations()
        {
            if (this.operations.Count == maxOperationsAllowed)
            {
                throw CosmosExceptionFactory.CreateBadRequestException(
                    $"Maximum number of operations allowed per PatchSpecification is {maxOperationsAllowed}.");
            }
        }
    }
}
