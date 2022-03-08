//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal interface IConsistencyValidator
    {
        public bool Validate(ConsistencyLevel accountLevelConsistency, 
            ConsistencyLevel requestOrClientLevelConsistency);
    }
}
