// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handler
{
    using System;
    using System.Collections.Concurrent;
    using Microsoft.Azure.Cosmos.Internal;

    internal sealed class AuthorizationTokenCache
    {
        private ConcurrentDictionary<string, Tuple<string, string, int>> Tokens = new ConcurrentDictionary<string, Tuple<string, string, int>>();

        public string GetOrAddToken(string tokenId, DocumentServiceRequest request, Func<Tuple<string, string>> authTokenFunc)
        {
            if (this.Tokens.TryGetValue(tokenId, out Tuple<string, string, int> value) && !this.IsExpired(value))
            {
                request.Headers[HttpConstants.HttpHeaders.XDate] = value.Item2;
                return value.Item1;
            }

            return this.Tokens.AddOrUpdate(
                tokenId,
            s =>
                {
                    Tuple<string, string> token = authTokenFunc();
                    return new Tuple<string, string, int>(token.Item1, token.Item2, Environment.TickCount);
                },
                (s, existing) =>
                {
                    if (!this.IsExpired(existing))
                    {
                        request.Headers[HttpConstants.HttpHeaders.XDate] = existing.Item2;
                        return existing;
                    }
                    else
                    {
                        Tuple<string, string> token = authTokenFunc();
                        return new Tuple<string, string, int>(token.Item1, token.Item2, Environment.TickCount);
                    }
                }).Item1;
        }

        private bool IsExpired(Tuple<string, string, int> value)
        {
            return value.Item3 < (Environment.TickCount - 400);
        }
    }
}