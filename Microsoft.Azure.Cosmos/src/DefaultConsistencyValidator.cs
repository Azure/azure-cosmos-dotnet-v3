//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class DefaultConsistencyValidator : IConsistencyValidator
    {
        public bool Validate(ConsistencyLevel accountLevelConsistency, ConsistencyLevel requestOrClientLevelConsistency)
        {
            return DefaultConsistencyValidator.IsValidConsistencyLevelOverwrite((Documents.ConsistencyLevel)accountLevelConsistency, (Documents.ConsistencyLevel)requestOrClientLevelConsistency);
        }

        private static bool IsValidConsistencyLevelOverwrite(Documents.ConsistencyLevel accountLevelConsistency, Documents.ConsistencyLevel requestOrClientLevelConsistency)
        {
            switch (accountLevelConsistency)
            {
                case Documents.ConsistencyLevel.Strong:
                    return requestOrClientLevelConsistency == Documents.ConsistencyLevel.Strong ||
                        requestOrClientLevelConsistency == Documents.ConsistencyLevel.BoundedStaleness ||
                        requestOrClientLevelConsistency == Documents.ConsistencyLevel.Session ||
                        requestOrClientLevelConsistency == Documents.ConsistencyLevel.Eventual ||
                        requestOrClientLevelConsistency == Documents.ConsistencyLevel.ConsistentPrefix;

                case Documents.ConsistencyLevel.BoundedStaleness:
                    return requestOrClientLevelConsistency == Documents.ConsistencyLevel.BoundedStaleness ||
                        requestOrClientLevelConsistency == Documents.ConsistencyLevel.Session ||
                        requestOrClientLevelConsistency == Documents.ConsistencyLevel.Eventual ||
                        requestOrClientLevelConsistency == Documents.ConsistencyLevel.ConsistentPrefix;

                case Documents.ConsistencyLevel.Session:
                case Documents.ConsistencyLevel.Eventual:
                case Documents.ConsistencyLevel.ConsistentPrefix:
                    return requestOrClientLevelConsistency == Documents.ConsistencyLevel.Session ||
                        requestOrClientLevelConsistency == Documents.ConsistencyLevel.Eventual ||
                        requestOrClientLevelConsistency == Documents.ConsistencyLevel.ConsistentPrefix;

                default:
                    throw new ArgumentException("backendConsistency");
            }
        }
    }
}
