//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    internal sealed class PatchOperationCore : PatchOperation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PatchOperationCore"/> class.
        /// </summary>
        /// <param name="operationType">Specifies the type of Patch operation.</param>
        /// <param name="path">Specifies the path to target location.</param>
        public PatchOperationCore(
            PatchOperationType operationType,
            string path)
        {
            this.OperationType = operationType;
            this.Path = string.IsNullOrWhiteSpace(path)
                ? throw new ArgumentNullException(nameof(path))
                : path;
        }

        public override PatchOperationType OperationType { get; }

        public override string Path { get; }
    }
}
