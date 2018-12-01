//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    internal static class ValidationHelpers
    {
        public static bool ValidateConsistencyLevel(ConsistencyLevel backendConsistency, ConsistencyLevel desiredConsistency)
        {
            switch (backendConsistency)
            {
                case ConsistencyLevel.Strong:
                    return desiredConsistency == ConsistencyLevel.Strong ||
                        desiredConsistency == ConsistencyLevel.BoundedStaleness ||
                        desiredConsistency == ConsistencyLevel.Session ||
                        desiredConsistency == ConsistencyLevel.Eventual ||
                        desiredConsistency == ConsistencyLevel.ConsistentPrefix;

                case ConsistencyLevel.BoundedStaleness:
                    return desiredConsistency == ConsistencyLevel.BoundedStaleness ||
                        desiredConsistency == ConsistencyLevel.Session ||
                        desiredConsistency == ConsistencyLevel.Eventual ||
                        desiredConsistency == ConsistencyLevel.ConsistentPrefix;

                case ConsistencyLevel.Session:
                case ConsistencyLevel.Eventual:
                case ConsistencyLevel.ConsistentPrefix:
                    return desiredConsistency == ConsistencyLevel.Session ||
                        desiredConsistency == ConsistencyLevel.Eventual ||
                        desiredConsistency == ConsistencyLevel.ConsistentPrefix;

                default:
                    throw new ArgumentException("backendConsistency");
            }
        }
    }
}
