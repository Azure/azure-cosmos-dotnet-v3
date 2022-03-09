//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Azure.Core;
    using Microsoft.Azure.Documents;

    internal static class ValidationHelpers
    {
        /// <summary>
        /// If isStrongReadAllowedOverEventualConsistency flag is true, it allows only "Strong Read with Eventual Consistency" else 
        /// It goes through normal validation where it doesn't allow strong consistency over weaker consistency.
        /// </summary>
        /// <param name="backendConsistency"> Account Level Consistency </param>
        /// <param name="desiredConsistency"> Request/Client Level Consistency</param>
        /// <param name="isStrongReadAllowedOverEventualConsistency"> Allows Strong Read with Eventual Write</param>
        /// <param name="operationType">  <see cref="OperationType"/> </param>
        /// <param name="resourceType"> <see cref="ResourceType"/> </param>
        /// <returns>true/false</returns>
        /// <exception cref="ArgumentException">Invalid Backend Consistency</exception>
        public static bool IsValidConsistencyLevelOverwrite(
                                    Cosmos.ConsistencyLevel backendConsistency, 
                                    Cosmos.ConsistencyLevel desiredConsistency, 
                                    bool isStrongReadAllowedOverEventualConsistency, 
                                    OperationType operationType,
                                    ResourceType resourceType)
        {
            return ValidationHelpers.IsValidConsistencyLevelOverwrite(
                                        backendConsistency: (Documents.ConsistencyLevel)backendConsistency,
                                        desiredConsistency: (Documents.ConsistencyLevel)desiredConsistency,
                                        isStrongReadAllowedOverEventualConsistency: isStrongReadAllowedOverEventualConsistency,
                                        operationType: operationType,
                                        resourceType: resourceType);
        }

        /// <summary>
        /// If isStrongReadAllowedOverEventualConsistency flag is true, it allows only "Strong Read with Eventual Consistency" else 
        /// It goes through normal validation where it doesn't allow strong consistency over weaker consistency.
        /// </summary>
        /// <param name="backendConsistency"> Account Level Consistency </param>
        /// <param name="desiredConsistency"> Request/Client Level Consistency</param>
        /// <param name="isStrongReadAllowedOverEventualConsistency"> Allows Strong Read with Eventual Write</param>
        /// <param name="operationType">  <see cref="OperationType"/> </param>
        /// <param name="resourceType"> <see cref="ResourceType"/> </param>
        /// <returns>true/false</returns>
        /// <exception cref="ArgumentException">Invalid Backend Consistency</exception>
        public static bool IsValidConsistencyLevelOverwrite(
                                    Documents.ConsistencyLevel backendConsistency, 
                                    Documents.ConsistencyLevel desiredConsistency,
                                    bool isStrongReadAllowedOverEventualConsistency,
                                    OperationType operationType,
                                    ResourceType resourceType)
        {
            if (isStrongReadAllowedOverEventualConsistency)
            {
                if ((operationType == OperationType.Read || operationType == OperationType.ReadFeed) &&
                    (resourceType == ResourceType.Document) &&
                    backendConsistency == Documents.ConsistencyLevel.Eventual &&
                    desiredConsistency == Documents.ConsistencyLevel.Strong)
                {
                    return true;
                }
            }

            switch (backendConsistency)
            {
                case Documents.ConsistencyLevel.Strong:
                    return desiredConsistency == Documents.ConsistencyLevel.Strong ||
                        desiredConsistency == Documents.ConsistencyLevel.BoundedStaleness ||
                        desiredConsistency == Documents.ConsistencyLevel.Session ||
                        desiredConsistency == Documents.ConsistencyLevel.Eventual ||
                        desiredConsistency == Documents.ConsistencyLevel.ConsistentPrefix;

                case Documents.ConsistencyLevel.BoundedStaleness:
                    return desiredConsistency == Documents.ConsistencyLevel.BoundedStaleness ||
                        desiredConsistency == Documents.ConsistencyLevel.Session ||
                        desiredConsistency == Documents.ConsistencyLevel.Eventual ||
                        desiredConsistency == Documents.ConsistencyLevel.ConsistentPrefix;

                case Documents.ConsistencyLevel.Session:
                case Documents.ConsistencyLevel.Eventual:
                case Documents.ConsistencyLevel.ConsistentPrefix:
                    return desiredConsistency == Documents.ConsistencyLevel.Session ||
                        desiredConsistency == Documents.ConsistencyLevel.Eventual ||
                        desiredConsistency == Documents.ConsistencyLevel.ConsistentPrefix;

                default:
                    throw new ArgumentException("Invalid Backend Consistency i.e." + backendConsistency);
            }
        }
    }
}
