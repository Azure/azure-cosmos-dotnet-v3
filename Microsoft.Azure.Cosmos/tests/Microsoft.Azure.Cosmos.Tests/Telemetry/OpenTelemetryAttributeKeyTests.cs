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
        [DataRow(false, null)]
        [DataRow(true, "HttpRequestException_NotFound_ResourceNotFound")]
        public void AppendErrorTypeForPopulateNetworkMeterDimensionsTests(bool throwException, string expectedResult)
        {
            OpenTelemetryAttributeKeys attributePopulator = new OpenTelemetryAttributeKeys();
            HttpStatusCode statusCode = throwException ? HttpStatusCode.NotFound : HttpStatusCode.OK;
            int subStatusCode = throwException ? (int)SubStatusCodes.ResourceNotFound : (int)SubStatusCodes.Unknown;
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
                responseMessage: responseMessage,
                exception: throwException ? httpRequestException : null,
                region: "East US");

            KeyValuePair<string, object>[] dimensions = attributePopulator.PopulateNetworkMeterDimensions(
                operationName: "create_database_if_not_exists",
                accountName: new Uri("https://test.com"),
                "test_container",
                "test_database",
                attributes,
                throwException ? cosmosException : null,
                null,
                null,
                httpStatistics);

            // Check error.type value
            KeyValuePair<string, object> errorType = dimensions.FirstOrDefault(d => d.Key == "error.type");
            Assert.IsNotNull(errorType);
            Assert.AreEqual(expectedResult, errorType.Value);
        }

        [TestMethod]
        [DataRow(false, null)]
        [DataRow(true, "CosmosException_NotFound_ResourceNotFound")]
        public void AppendErrorTypeForPopulateOperationMeterDimensionsTests(bool throwException, string expectedResult)
        {
            OpenTelemetryAttributeKeys attributePopulator = new OpenTelemetryAttributeKeys();
            OpenTelemetryAttributes attributes = new OpenTelemetryAttributes
            {
                ConsistencyLevel = ConsistencyLevel.Strong.ToString(),
                StatusCode = throwException ? HttpStatusCode.NotFound : HttpStatusCode.OK,
                SubStatusCode = throwException ? (int)SubStatusCodes.ResourceNotFound : (int)SubStatusCodes.Unknown,
            };

            Exception innerException = new NotFoundException("Not found");
            CosmosException cosmosException = new CosmosException(
                statusCode: HttpStatusCode.NotFound,
                message: "Not found",
                stackTrace: null,
                headers: new Headers { { "x-ms-substatus", ((int)SubStatusCodes.ResourceNotFound).ToString() } },
                trace: null,
                error: null,
                innerException: innerException);

            KeyValuePair<string, object>[] dimensions = attributePopulator.PopulateOperationMeterDimensions(
                operationName: "create_database_if_not_exists",
                containerName: "Items",
                databaseName: "ToDoList",
                accountName: new Uri("https://test.com"),
                attributes: attributes,
                ex: throwException ? cosmosException : null,
                optionFromRequest: null);

            // Check error.type value
            KeyValuePair<string, object> errorType = dimensions.FirstOrDefault(d => d.Key == "error.type");
            Assert.IsNotNull(errorType);
            Assert.AreEqual(expectedResult, errorType.Value);
        }

        [TestMethod]
        public void AppendErrorTypeForPopulateOperationMeterDimensionsTests_AbnormalCase()
        {
            OpenTelemetryAttributeKeys attributePopulator = new OpenTelemetryAttributeKeys();

            Exception exception = new NotFoundException("Not found");

            KeyValuePair<string, object>[] dimensions = attributePopulator.PopulateOperationMeterDimensions(
                operationName: "create_database_if_not_exists",
                containerName: "Items",
                databaseName: "ToDoList",
                accountName: new Uri("https://test.com"),
                attributes: null,
                ex: exception,
                optionFromRequest: null);

            // Check error.type value
            KeyValuePair<string, object> errorType = dimensions.FirstOrDefault(d => d.Key == "error.type");
            Assert.IsNotNull(errorType);
            Assert.AreEqual("NotFoundException__", errorType.Value);
        }
    }
}
