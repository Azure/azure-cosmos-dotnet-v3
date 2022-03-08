//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal interface IConsistencyValidator
    {
        /// <summary>
        /// It validates if particular consistency combination is valid for a given Operation Type.
        /// 
        /// SDK validates consistency at 2 places
        /// 1) During initialization of client, at that time operationType will null 
        /// 2) in request Invoke handler
        /// </summary>
        /// <param name="accountLevelConsistency"></param>
        /// <param name="requestOrClientLevelConsistency"></param>
        /// <param name="operationType"></param>
        /// <param name="resourceType"></param>
        /// <returns>true/false</returns>
        public bool Validate(ConsistencyLevel accountLevelConsistency, 
            ConsistencyLevel requestOrClientLevelConsistency, 
            Documents.OperationType? operationType = null,
            Documents.ResourceType? resourceType = null);
    }
}
