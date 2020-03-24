// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;

    internal sealed class ChangeFeedTokenInternal : ChangeFeedToken
    {
        public IChangeFeedToken ChangeFeedToken { get; }
        public ChangeFeedTokenInternal(IChangeFeedToken token)
        {
            this.ChangeFeedToken = token;
        }

        public static bool TryCreateFromString(
            string toStringValue,
            out ChangeFeedToken parsedToken)
        {
            if (FeedTokenEPKRange.TryParseInstance(toStringValue, out FeedTokenEPKRange epkToken))
            {
                parsedToken = new ChangeFeedTokenInternal(epkToken);
                return true;
            }

            if (FeedTokenPartitionKey.TryParseInstance(toStringValue, out FeedTokenPartitionKey pkRangeToken))
            {
                parsedToken = new ChangeFeedTokenInternal(pkRangeToken);
                return true;
            }

            if (FeedTokenPartitionKeyRange.TryParseInstance(toStringValue, out FeedTokenPartitionKeyRange pkToken))
            {
                parsedToken = new ChangeFeedTokenInternal(pkToken);
                return true;
            }

            parsedToken = null;
            return false;
        }

        public override IReadOnlyList<ChangeFeedToken> Scale()
        {
            List<ChangeFeedToken> scaleTokens = new List<ChangeFeedToken>();
            foreach (IChangeFeedToken changeFeedToken in this.ChangeFeedToken.Scale())
            {
                scaleTokens.Add(new ChangeFeedTokenInternal(changeFeedToken));
            }

            return scaleTokens;
            
        }

        public override string ToString() => this.ChangeFeedToken.ToString();
    }
}
