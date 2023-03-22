// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;

    internal sealed class ResumeValueToCosmosElementConverter
    {
        public static CosmosElement Convert(SqlQueryResumeInfo.ResumeValue resumeValue)
        {
            return resumeValue switch
            {
                SqlQueryResumeInfo.UndefinedResumeValue => CosmosArray.Create(new List<CosmosElement>()),
                SqlQueryResumeInfo.NullResumeValue => CosmosNull.Create(),
                SqlQueryResumeInfo.BooleanResumeValue booleanValue => CosmosBoolean.Create(booleanValue.Value),
                SqlQueryResumeInfo.NumberResumeValue numberValue => CosmosNumber64.Create(numberValue.Value),
                SqlQueryResumeInfo.StringResumeValue stringValue => CosmosString.Create(stringValue.Value),
                SqlQueryResumeInfo.ArrayResumeValue arrayValue => CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { SqlQueryResumeInfo.ResumeValue.PropertyNames.Type, CosmosString.Create(SqlQueryResumeInfo.ResumeValue.PropertyNames.ArrayType) },
                        { SqlQueryResumeInfo.ResumeValue.PropertyNames.Low, CosmosNumber64.Create(arrayValue.HashValue.GetLow()) },
                        { SqlQueryResumeInfo.ResumeValue.PropertyNames.High, CosmosNumber64.Create(arrayValue.HashValue.GetHigh()) }
                    }),
                SqlQueryResumeInfo.ObjectResumeValue objectValue => CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { SqlQueryResumeInfo.ResumeValue.PropertyNames.Type, CosmosString.Create(SqlQueryResumeInfo.ResumeValue.PropertyNames.ObjectType) },
                        { SqlQueryResumeInfo.ResumeValue.PropertyNames.Low, CosmosNumber64.Create(objectValue.HashValue.GetLow()) },
                        { SqlQueryResumeInfo.ResumeValue.PropertyNames.High, CosmosNumber64.Create(objectValue.HashValue.GetHigh()) }
                    }),
                _ => throw new ArgumentException($"Invalid {nameof(SqlQueryResumeInfo.ResumeValue)} type."),
            };
        }
    }
}
