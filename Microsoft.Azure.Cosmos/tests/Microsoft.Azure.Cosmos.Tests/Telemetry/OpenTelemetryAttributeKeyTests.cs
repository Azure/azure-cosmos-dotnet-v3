//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="OpenTelemetryAttributeKeys"/>.
    /// </summary>
    [TestClass]
    public class OpenTelemetryAttributeKeysTests
    {
        [TestMethod]
        [DataRow(HttpStatusCode.NotFound, SubStatusCodes.Unknown, "NotFound")]
        [DataRow(HttpStatusCode.OK, SubStatusCodes.CollectionCreateInProgress, "CollectionCreateInProgress")]
        [DataRow(HttpStatusCode.OK, SubStatusCodes.Unknown, nameof(HttpRequestException))]
        public void AppendErrorTypeForPopulateNetworkMeterDimensionsTests(HttpStatusCode statusCode, int subStatusCode, string expectedResult)
        {
            OpenTelemetryAttributeKeys attributePopulator = new OpenTelemetryAttributeKeys();
            OpenTelemetryAttributes attributes = new OpenTelemetryAttributes
            {
                ConsistencyLevel = ConsistencyLevel.Strong.ToString(),
                StatusCode = statusCode,
                SubStatusCode = subStatusCode,
            };

            CosmosException cosmosException = new CosmosException(
                message: "test",
                statusCode: statusCode,
                subStatusCode: subStatusCode,
                activityId: Guid.NewGuid().ToString(),
                requestCharge: 1.1);

            // Mock http response message
            HttpResponseMessage responseMessage = new HttpResponseMessage(statusCode);
            responseMessage.Headers.Add("x-ms-substatus", subStatusCode.ToString());

            // Mock http exception
            HttpRequestException httpRequestException = new HttpRequestException("test");

            ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics httpStatistics = new ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics(
                requestStartTime: DateTime.MinValue,
                requestEndTime: DateTime.MinValue,
                requestUri: new Uri("https://test.com"),
                httpMethod: HttpMethod.Post,
                resourceType: ResourceType.Database,
                responseMessage: null,
                exception: httpRequestException,
                region: "East US");

            KeyValuePair<string, object>[] dimensions = attributePopulator.PopulateNetworkMeterDimensions(
                operationName: "create_database_if_not_exists",
                accountName: new Uri("https://test.com"),
                "test_container",
                "test_database",
                attributes,
                cosmosException,
                null,
                null,
                httpStatistics);

            // Check error.type value
            KeyValuePair<string, object> errorType = dimensions.FirstOrDefault(d => d.Key == "error.type");
            Assert.IsNotNull(errorType);
            Assert.AreEqual(expectedResult, errorType.Value);
        }

        [TestMethod]
        [DataRow(null, null, false, "_OTHER")]
        [DataRow(null, null, true, nameof(Exception))]
        [DataRow(HttpStatusCode.OK, (int)SubStatusCodes.Unknown, true, nameof(Exception))]
        [DataRow(HttpStatusCode.OK, (int)SubStatusCodes.ResourceNotFound, true, nameof(SubStatusCodes.ResourceNotFound))]
        [DataRow(HttpStatusCode.Conflict, (int)SubStatusCodes.Unknown, true, nameof(HttpStatusCode.Conflict))]
        [DataRow(HttpStatusCode.Conflict, (int)SubStatusCodes.ResourceNotFound, true, nameof(SubStatusCodes.ResourceNotFound))]
        public void AppendErrorTypeForPopulateOperationMeterDimensionsTests(HttpStatusCode? statusCode, int? subStatusCode, bool passException, string expectedResult)
        {
            OpenTelemetryAttributeKeys attributePopulator = new OpenTelemetryAttributeKeys();
            OpenTelemetryAttributes attributes = null;

            if (statusCode.HasValue)
            {
                attributes ??= new OpenTelemetryAttributes();
                attributes.StatusCode = statusCode.Value;
            }

            if (subStatusCode.HasValue)
            {
                attributes ??= new OpenTelemetryAttributes();
                attributes.SubStatusCode = subStatusCode.Value;
            }

            Exception innerException = new Exception("Not found");
            Headers headers = new Headers
            {
                { "x-ms-substatus", subStatusCode?.ToString() ?? SubStatusCodes.Unknown.ToString() }
            };
            CosmosException cosmosException = new CosmosException(
                statusCode: statusCode ?? HttpStatusCode.OK,
                message: "Not found",
                stackTrace: null,
                headers: headers,
                trace: null,
                error: null,
                innerException: innerException);

            KeyValuePair<string, object>[] dimensions = attributePopulator.PopulateOperationMeterDimensions(
                operationName: "create_database_if_not_exists",
                containerName: "Items",
                databaseName: "ToDoList",
                accountName: new Uri("https://test.com"),
                attributes: attributes,
                ex: passException ? cosmosException : null,
                optionFromRequest: null);

            // Check error.type value
            KeyValuePair<string, object> errorType = dimensions.FirstOrDefault(d => d.Key == "error.type");
            Assert.IsNotNull(errorType);
            Assert.AreEqual(expectedResult, errorType.Value);
        }
    }
}
