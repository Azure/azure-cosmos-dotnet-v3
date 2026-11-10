//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Documents;

    internal static class ChangeFeedHelper
    {
        internal static ChangeFeedQuerySpec CreateQuerySpec(string queryText)
        {
            if (queryText == null)
            {
                throw new ArgumentNullException(nameof(queryText));
            }

            if (string.IsNullOrWhiteSpace(queryText))
            {
                throw new ArgumentException("Query text cannot be empty or whitespace.", nameof(queryText));
            }

            return new ChangeFeedQuerySpec(queryText);
        }

        internal static string GetQueryFilterHash(ChangeFeedQuerySpec querySpec)
        {
            if (querySpec == null)
            {
                return null;
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(querySpec.QueryText)));
            }
        }

        internal static bool IsChangeFeedWithQueryRequest(
           OperationType operationType,
           bool hasPayload)
        {
            // ChangeFeed with payload is a CF with query support and will
            // be a query POST request.
            return operationType == OperationType.ReadFeed && hasPayload;
        }

    }
}
