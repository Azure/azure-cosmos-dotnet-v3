//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;

    internal class CosmosTroubleShootingLink
    {
        internal string Link { get; }
        internal int StatusCode { get; }
        internal int? SubStatusCode { get; }

        private CosmosTroubleShootingLink(
            int statusCode,
            int? subStatusCode,
            string link)
        {
            this.StatusCode = statusCode;
            this.SubStatusCode = subStatusCode;
            this.Link = link ?? throw new ArgumentNullException(nameof(link));
        }

        internal static readonly CosmosTroubleShootingLink TransportException = new CosmosTroubleShootingLink(
            statusCode: (int)HttpStatusCode.ServiceUnavailable,
            subStatusCode: null,
            link: "http");

        internal static readonly CosmosTroubleShootingLink NotFound = new CosmosTroubleShootingLink(
            statusCode: (int)HttpStatusCode.NotFound,
            subStatusCode: null,
            link: "http");

        internal static readonly CosmosTroubleShootingLink ToManyRequestRUBudgetExceeded = new CosmosTroubleShootingLink(
            statusCode: 429,
            subStatusCode: 3200,
            link: "http");

        internal static readonly CosmosTroubleShootingLink ToManyRequestStoredProcedureConcurrency = new CosmosTroubleShootingLink(
            statusCode: 429,
            subStatusCode: 3084,
            link: "http");

        internal static readonly CosmosTroubleShootingLink NotModified = new CosmosTroubleShootingLink(
            statusCode: 304,
            subStatusCode: null,
            link: "http");

        internal static readonly CosmosTroubleShootingLink Conflict = new CosmosTroubleShootingLink(
            statusCode: 409,
            subStatusCode: null,
            link: "http");

        internal static readonly CosmosTroubleShootingLink BadRequestConfigurationNameNotFound = new CosmosTroubleShootingLink(
            statusCode: 400,
            subStatusCode: 1004,
            link: "http");

        internal static readonly CosmosTroubleShootingLink PreconditionFailed = new CosmosTroubleShootingLink(
            statusCode: 412,
            subStatusCode: null,
            link: "http");

        internal static readonly CosmosTroubleShootingLink RetryWith = new CosmosTroubleShootingLink(
            statusCode: 449,
            subStatusCode: null,
            link: "http");

        internal static readonly CosmosTroubleShootingLink GonePartitionKeyRangeGone = new CosmosTroubleShootingLink(
            statusCode: 410,
            subStatusCode: 1002,
            link: "http");

        internal static readonly CosmosTroubleShootingLink Forbidden = new CosmosTroubleShootingLink(
            statusCode: 403,
            subStatusCode: null,
            link: "http");
    }
}
