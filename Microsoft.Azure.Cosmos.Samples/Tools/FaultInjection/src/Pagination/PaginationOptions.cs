﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Documents;

    internal abstract class PaginationOptions
    {
        protected static readonly ImmutableHashSet<string> bannedAdditionalHeaders = new HashSet<string>()
        {
            HttpConstants.HttpHeaders.PageSize,
            HttpConstants.HttpHeaders.ContentSerializationFormat,
        }.ToImmutableHashSet<string>();

        private static readonly ImmutableDictionary<string, string> EmptyDictionary = new Dictionary<string, string>()
            .ToImmutableDictionary<string, string>();

        protected PaginationOptions(
            int? pageSizeLimit = null,
            JsonSerializationFormat? jsonSerializationFormat = null,
            Dictionary<string, string> additionalHeaders = null)
        {
            this.PageSizeLimit = pageSizeLimit;
            this.JsonSerializationFormat = jsonSerializationFormat;
            this.AdditionalHeaders = additionalHeaders != null ? additionalHeaders.ToImmutableDictionary<string, string>() : EmptyDictionary;

            foreach (string key in this.AdditionalHeaders.Keys)
            {
                if (bannedAdditionalHeaders.Contains(key) || this.BannedAdditionalHeaders.Contains(key))
                {
                    throw new ArgumentOutOfRangeException($"The following http header is not allowed: '{key}'");
                }
            }
        }

        public int? PageSizeLimit { get; }

        public JsonSerializationFormat? JsonSerializationFormat { get; }

        public ImmutableDictionary<string, string> AdditionalHeaders { get; }

        protected abstract ImmutableHashSet<string> BannedAdditionalHeaders { get; }
    }
}
