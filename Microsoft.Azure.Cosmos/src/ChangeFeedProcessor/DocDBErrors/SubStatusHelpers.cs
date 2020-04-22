//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.DocDBErrors
{
    using System.Globalization;
    using Microsoft.Azure.Documents;

    internal static class SubStatusHelpers
    {
        public static SubStatusCodes GetSubStatusCode(this DocumentClientException exception)
        {
            const string subStatusHeaderName = "x-ms-substatus";

            string valueSubStatus = exception.ResponseHeaders.Get(subStatusHeaderName);
            if (!string.IsNullOrEmpty(valueSubStatus))
            {
                if (int.TryParse(valueSubStatus, NumberStyles.Integer, CultureInfo.InvariantCulture, out int subStatusCode))
                    return (SubStatusCodes)subStatusCode;
            }

            return SubStatusCodes.Unknown;
        }
    }
}