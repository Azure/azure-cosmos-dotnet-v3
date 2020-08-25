//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading.Tasks;

    internal static class RequestHelper
    {
        public static ConsistencyLevel GetConsistencyLevelToUse(IServiceConfigurationReader serviceConfigReader, DocumentServiceRequest request)
        {
            ConsistencyLevel consistencyLevelToUse = serviceConfigReader.DefaultConsistencyLevel;

            string requestConsistencyLevelHeaderValue = request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel];

            if (!string.IsNullOrEmpty(requestConsistencyLevelHeaderValue))
            {
                ConsistencyLevel requestConsistencyLevel;

                if (!Enum.TryParse<ConsistencyLevel>(requestConsistencyLevelHeaderValue, out requestConsistencyLevel))
                {
                    throw new BadRequestException(
                        string.Format(CultureInfo.CurrentUICulture,
                        RMResources.InvalidHeaderValue,
                        requestConsistencyLevelHeaderValue,
                        HttpConstants.HttpHeaders.ConsistencyLevel));
                }

                consistencyLevelToUse = requestConsistencyLevel;
            }

            return consistencyLevelToUse;
        }
    }
}
