// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;
    using static Microsoft.Azure.Cosmos.Query.Core.SqlQueryResumeFilter;

    internal sealed class ResumeValueComparer
    {
        public static int Compare(CosmosElement orderByResult, ResumeValue resumeValue)
        {
            switch (resumeValue)
            {
                case UndefinedResumeValue:
                    return ItemComparer.Instance.Compare(CosmosUndefined.Create(), orderByResult);

                case NullResumeValue:
                    return ItemComparer.Instance.Compare(CosmosNull.Create(), orderByResult);

                case BooleanResumeValue booleanValue:
                    return ItemComparer.Instance.Compare(CosmosBoolean.Create(booleanValue.Value), orderByResult);

                case NumberResumeValue numberValue:
                    return ItemComparer.Instance.Compare(CosmosNumber64.Create(numberValue.Value), orderByResult);

                case StringResumeValue stringValue:
                    return ItemComparer.Instance.Compare(CosmosString.Create(stringValue.Value), orderByResult);

                case ArrayResumeValue arrayValue:
                    {
                        // If the order by result is also of array type, then compare the hash values
                        // For other types create an empty array and call CosmosElement comparer which
                        // will take care of ordering based on types.
                        if (orderByResult is CosmosArray arrayResult)
                        {
                            return UInt128BinaryComparer.Singleton.Compare(arrayValue.HashValue, DistinctHash.GetHash(arrayResult));
                        }
                        else
                        {
                            return ItemComparer.Instance.Compare(CosmosArray.Empty, orderByResult);
                        }
                    }

                case ObjectResumeValue objectValue:
                    {
                        // If the order by result is also of object type, then compare the hash values
                        // For other types create an empty object and call CosmosElement comparer which
                        // will take care of ordering based on types.
                        if (orderByResult is CosmosObject objectResult)
                        {
                            // same type so compare the hash values
                            return UInt128BinaryComparer.Singleton.Compare(objectValue.HashValue, DistinctHash.GetHash(objectResult));
                        }
                        else
                        {
                            return ItemComparer.Instance.Compare(CosmosObject.Create(new Dictionary<string, CosmosElement>()), orderByResult);
                        }
                    }

                default:
                    throw new ArgumentException($"Invalid {nameof(ResumeValue)} type.");
            }
        }
    }
}
