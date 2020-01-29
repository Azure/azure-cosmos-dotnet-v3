//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;

    internal class CosmosTroubleShootingLinks
    {
        internal string Link { get; }
        internal int StatusCode { get; }
        internal int? SubStatusCode { get; }

        private CosmosTroubleShootingLinks(
            int statusCode,
            int? subStatusCode,
            string link)
        {
            this.StatusCode = statusCode;
            this.SubStatusCode = subStatusCode;
            this.Link = link ?? throw new ArgumentNullException(nameof(link));
        }

        internal static readonly CosmosTroubleShootingLinks NotFound = new CosmosTroubleShootingLinks(
            statusCode: (int)HttpStatusCode.NotFound,
            subStatusCode: null,
            link: "http");

        internal static readonly CosmosTroubleShootingLinks ToManyRequestRUBudgetExceeded = new CosmosTroubleShootingLinks(
            statusCode: 429,
            subStatusCode: 3200,
            link: "http");

        internal static readonly CosmosTroubleShootingLinks ToManyRequestStoredProcedureConcurrency = new CosmosTroubleShootingLinks(
            statusCode: 429,
            subStatusCode: 3084,
            link: "http");

        internal static readonly CosmosTroubleShootingLinks NotModified = new CosmosTroubleShootingLinks(
            statusCode: 304,
            subStatusCode: null,
            link: "http");

        internal static readonly CosmosTroubleShootingLinks Conflict = new CosmosTroubleShootingLinks(
            statusCode: 409,
            subStatusCode: null,
            link: "http");

        internal static readonly CosmosTroubleShootingLinks BadRequestConfigurationNameNotFound = new CosmosTroubleShootingLinks(
            statusCode: 400,
            subStatusCode: 1004,
            link: "http");

        internal static readonly CosmosTroubleShootingLinks PreconditionFailed = new CosmosTroubleShootingLinks(
            statusCode: 412,
            subStatusCode: null,
            link: "http");

        internal static readonly CosmosTroubleShootingLinks RetryWith = new CosmosTroubleShootingLinks(
            statusCode: 449,
            subStatusCode: null,
            link: "http");

        internal static readonly CosmosTroubleShootingLinks GonePartitionKeyRangeGone = new CosmosTroubleShootingLinks(
            statusCode: 410,
            subStatusCode: 1002,
            link: "http");

        internal static readonly CosmosTroubleShootingLinks Forbidden = new CosmosTroubleShootingLinks(
            statusCode: 403,
            subStatusCode: null,
            link: "http");
    }
}
