//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    internal class DefaultConsistencyValidator : IConsistencyValidator
    {
        public bool Validate(ConsistencyLevel accountLevelConsistency, ConsistencyLevel requestOrClientLevelConsistency)
        {
            Documents.ConsistencyLevel accountLevelDocumentConsistency = (Documents.ConsistencyLevel)accountLevelConsistency;
            Documents.ConsistencyLevel requestOrClientLevelDocumentConsistency = (Documents.ConsistencyLevel)requestOrClientLevelConsistency;
            
            switch (accountLevelDocumentConsistency)
            {
                case Documents.ConsistencyLevel.Strong:
                    return requestOrClientLevelDocumentConsistency == Documents.ConsistencyLevel.Strong ||
                        requestOrClientLevelDocumentConsistency == Documents.ConsistencyLevel.BoundedStaleness ||
                        requestOrClientLevelDocumentConsistency == Documents.ConsistencyLevel.Session ||
                        requestOrClientLevelDocumentConsistency == Documents.ConsistencyLevel.Eventual ||
                        requestOrClientLevelDocumentConsistency == Documents.ConsistencyLevel.ConsistentPrefix;

                case Documents.ConsistencyLevel.BoundedStaleness:
                    return requestOrClientLevelDocumentConsistency == Documents.ConsistencyLevel.BoundedStaleness ||
                        requestOrClientLevelDocumentConsistency == Documents.ConsistencyLevel.Session ||
                        requestOrClientLevelDocumentConsistency == Documents.ConsistencyLevel.Eventual ||
                        requestOrClientLevelDocumentConsistency == Documents.ConsistencyLevel.ConsistentPrefix;

                case Documents.ConsistencyLevel.Session:
                case Documents.ConsistencyLevel.Eventual:
                case Documents.ConsistencyLevel.ConsistentPrefix:
                    return requestOrClientLevelDocumentConsistency == Documents.ConsistencyLevel.Session ||
                        requestOrClientLevelDocumentConsistency == Documents.ConsistencyLevel.Eventual ||
                        requestOrClientLevelDocumentConsistency == Documents.ConsistencyLevel.ConsistentPrefix;

                default:
                    throw new ArgumentException("backendConsistency");
            }
        }
    }
}
