// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    [JsonConverter(typeof(QueryFeedTokenInternalConverter))]
    internal sealed class QueryFeedTokenInternal : QueryFeedToken
    {
        public readonly IQueryFeedToken QueryFeedToken;

        public readonly QueryDefinition QueryDefinition;

        public QueryFeedTokenInternal(
            IQueryFeedToken token,
            QueryDefinition queryDefinition)
        {
            this.QueryFeedToken = token;
            this.QueryDefinition = queryDefinition;
        }

        public static bool TryParse(
            string toStringValue,
            out QueryFeedToken parsedToken)
        {
            try
            {
                parsedToken = JsonConvert.DeserializeObject<QueryFeedTokenInternal>(toStringValue);
                return true;
            }
            catch
            {
                parsedToken = null;
                return false;
            }
        }

        public override IReadOnlyList<QueryFeedToken> Scale()
        {
            List<QueryFeedToken> scaleTokens = new List<QueryFeedToken>();
            foreach (IQueryFeedToken queryFeedToken in this.QueryFeedToken.Scale())
            {
                scaleTokens.Add(new QueryFeedTokenInternal(queryFeedToken, this.QueryDefinition));
            }

            return scaleTokens;

        }

        public override string ToString() => JsonConvert.SerializeObject(this);
    }
}
