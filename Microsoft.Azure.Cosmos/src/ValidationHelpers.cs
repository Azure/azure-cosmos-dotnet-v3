//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    internal static class ValidationHelpers
    {
        /// <summary>
        /// If isStrongReadAllowedOverEventualConsistency flag is true, it allows only "Strong Read with Eventual Consistency Write". 
        /// It goes through a validation where it doesn't allow strong consistency over weaker consistency.
        /// </summary>
        /// <param name="backendConsistency"> Account Level Consistency </param>
        /// <param name="desiredConsistency"> Request/Client Level Consistency</param>
        /// <param name="isLocalQuorumConsistency"> Allows Strong Read with Eventual Write</param>
        /// <param name="operationType"> <see cref="OperationType"/> </param>
        /// <param name="resourceType"> <see cref="ResourceType"/> </param>
        /// <returns>true/false</returns>
        /// <exception cref="ArgumentException">Invalid Backend Consistency</exception>
        public static bool IsValidConsistencyLevelOverwrite(
                                    Cosmos.ConsistencyLevel backendConsistency, 
                                    Cosmos.ConsistencyLevel desiredConsistency, 
                                    bool isLocalQuorumConsistency, 
                                    OperationType operationType,
                                    ResourceType resourceType)
        {
            return ValidationHelpers.IsValidConsistencyLevelOverwrite(
                                        backendConsistency: (Documents.ConsistencyLevel)backendConsistency,
                                        desiredConsistency: (Documents.ConsistencyLevel)desiredConsistency,
                                        isLocalQuorumConsistency: isLocalQuorumConsistency,
                                        operationType: operationType,
                                        resourceType: resourceType);
        }

        /// <summary>
        /// If isStrongReadAllowedOverEventualConsistency flag is true, it allows only "Strong Read with Eventual Consistency Write". 
        /// It goes through a validation where it doesn't allow strong consistency over weaker consistency.
        /// </summary>
        /// <param name="backendConsistency"> Account Level Consistency </param>
        /// <param name="desiredConsistency"> Request/Client Level Consistency</param>
        /// <param name="isLocalQuorumConsistency"> Allows Strong Read with Eventual Write</param>
        /// <param name="operationType">  <see cref="OperationType"/> </param>
        /// <param name="resourceType"> <see cref="ResourceType"/> </param>
        /// <returns>true/false</returns>
        /// <exception cref="ArgumentException">Invalid Backend Consistency</exception>
        public static bool IsValidConsistencyLevelOverwrite(
                                    Documents.ConsistencyLevel backendConsistency,
                                    Documents.ConsistencyLevel desiredConsistency,
                                    bool isLocalQuorumConsistency,
                                    OperationType? operationType,
                                    ResourceType? resourceType)
        {
            if (isLocalQuorumConsistency && 
                    ValidationHelpers.IsLocalQuorumConsistency(
                            backendConsistency: backendConsistency,
                            desiredConsistency: desiredConsistency,
                            operationType: operationType,
                            resourceType: resourceType))
            {
                    return true;
            }

            return ValidationHelpers.IsValidConsistencyLevelOverwrite(
                                        backendConsistency: backendConsistency,
                                        desiredConsistency: desiredConsistency);
        }

        /// <summary>
        /// It doesn't allow strong consistency over weaker consistency.
        /// </summary>
        /// <param name="backendConsistency"> Account Level Consistency </param>
        /// <param name="desiredConsistency"> Request/Client Level Consistency</param>
        /// <returns>true/false</returns>
        /// <exception cref="ArgumentException">Invalid Backend Consistency</exception>
        private static bool IsValidConsistencyLevelOverwrite(
                                Documents.ConsistencyLevel backendConsistency, 
                                Documents.ConsistencyLevel desiredConsistency)
        {
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
                    throw new ArgumentException("Invalid Backend Consistency i.e. " + backendConsistency);
            }
        }

        /// <summary>
        /// Condition to check strong read with eventual write
        /// </summary>
        /// <param name="backendConsistency"></param>
        /// <param name="desiredConsistency"></param>
        /// <param name="operationType"></param>
        /// <param name="resourceType"></param>
        /// <returns>true/false</returns>
        private static bool IsLocalQuorumConsistency(Documents.ConsistencyLevel backendConsistency,
                                Documents.ConsistencyLevel desiredConsistency,
                                OperationType? operationType,
                                ResourceType? resourceType)
        {
            if (backendConsistency != Documents.ConsistencyLevel.Eventual)
            {
                return false;
            }

            if (desiredConsistency != Documents.ConsistencyLevel.Strong)
            {
                return false;
            }

            if (!resourceType.HasValue || 
                    (resourceType.HasValue && resourceType != ResourceType.Document))
            {
                return false;
            }

            if (!operationType.HasValue || 
                    (operationType.HasValue && !(operationType == OperationType.Read || operationType == OperationType.ReadFeed)))
            {
                return false;
            }

            return true;
        }
    }
}
