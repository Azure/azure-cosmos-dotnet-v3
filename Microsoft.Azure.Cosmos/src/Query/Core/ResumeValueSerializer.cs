// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;

    internal sealed class ResumeValueSerializer
    {
        public static CosmosElement ToCosmosElement(SqlQueryResumeFilter.ResumeValue resumeValue)
        {
            return resumeValue switch
            {
                SqlQueryResumeFilter.UndefinedResumeValue => CosmosArray.Create(new List<CosmosElement>()),
                SqlQueryResumeFilter.NullResumeValue => CosmosNull.Create(),
                SqlQueryResumeFilter.BooleanResumeValue booleanValue => CosmosBoolean.Create(booleanValue.Value),
                SqlQueryResumeFilter.NumberResumeValue numberValue => CosmosNumber64.Create(numberValue.Value),
                SqlQueryResumeFilter.StringResumeValue stringValue => CosmosString.Create(stringValue.Value),
                SqlQueryResumeFilter.ArrayResumeValue arrayValue => CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { SqlQueryResumeFilter.ResumeValue.PropertyNames.Type, CosmosString.Create(SqlQueryResumeFilter.ResumeValue.PropertyNames.ArrayType) },
                        { SqlQueryResumeFilter.ResumeValue.PropertyNames.Low, CosmosNumber64.Create(arrayValue.HashValue.GetLow()) },
                        { SqlQueryResumeFilter.ResumeValue.PropertyNames.High, CosmosNumber64.Create(arrayValue.HashValue.GetHigh()) }
                    }),
                SqlQueryResumeFilter.ObjectResumeValue objectValue => CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { SqlQueryResumeFilter.ResumeValue.PropertyNames.Type, CosmosString.Create(SqlQueryResumeFilter.ResumeValue.PropertyNames.ObjectType) },
                        { SqlQueryResumeFilter.ResumeValue.PropertyNames.Low, CosmosNumber64.Create(objectValue.HashValue.GetLow()) },
                        { SqlQueryResumeFilter.ResumeValue.PropertyNames.High, CosmosNumber64.Create(objectValue.HashValue.GetHigh()) }
                    }),
                _ => throw new ArgumentException($"Invalid {nameof(SqlQueryResumeFilter.ResumeValue)} type."),
            };
        }
    }
}
