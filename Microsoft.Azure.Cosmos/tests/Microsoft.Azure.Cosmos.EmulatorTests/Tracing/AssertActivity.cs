//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.Diagnostics;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    internal static class AssertActivity
    {
        public static void IsValidOperationActivity(Activity activity)
        {
            if (activity.OperationName.StartsWith("Operation.", StringComparison.OrdinalIgnoreCase))
            {
                Assert.IsFalse(string.IsNullOrEmpty(activity.GetTagItem("db.cosmosdb.connection_mode").ToString()), $"connection mode is empty for {activity.OperationName}");

                if (activity.GetTagItem("db.cosmosdb.connection_mode").ToString() == ConnectionMode.Gateway.ToString())
                {
                    Assert.AreEqual(ActivityKind.Internal, activity.Kind, $" Actual Kind is {activity.Kind} but expected is {ActivityKind.Internal} for {activity.OperationName}");
                }
                else if (activity.GetTagItem("db.cosmosdb.connection_mode").ToString() == ConnectionMode.Direct.ToString())
                {
                    Assert.AreEqual(ActivityKind.Client, activity.Kind, $" Actual Kind is {activity.Kind} but expected is {ActivityKind.Client} for {activity.OperationName}");
                }

                IList<string> expectedTags = new List<string>
                {
                     "az.namespace",
                     "az.schema_url",
                     "kind",
                     "db.system",
                     "db.namespace",
                     "db.operation.name",
                     "server.address",
                     "server.port",
                     "db.cosmosdb.client_id",
                     "user_agent.original",
                     "db.cosmosdb.connection_mode",
                     "db.collection.name",
                     "db.cosmosdb.request_content_length",
                     "db.cosmosdb.response_content_length",
                     "db.response.status_code",
                     "db.cosmosdb.sub_status_code",
                     "db.cosmosdb.request_charge",
                     "db.cosmosdb.regions_contacted",
                     "db.cosmosdb.row_count",
                     "db.operation.batch.size",
                     "db.cosmosdb.activity_id",
                     "db.cosmosdb.correlated_activity_id",
                     "db.cosmosdb.consistency_level",
                     "exception.type",
                     "exception.message",
                     "exception.stacktrace",
                     "db.query.text",
                     "error.type",
                     AppInsightClassicAttributeKeys.DbName,
                     AppInsightClassicAttributeKeys.ContainerName,
                     AppInsightClassicAttributeKeys.DbOperation,
                     AppInsightClassicAttributeKeys.ServerAddress,
                     AppInsightClassicAttributeKeys.StatusCode,
                     AppInsightClassicAttributeKeys.UserAgent,
                     AppInsightClassicAttributeKeys.MachineId,
                     AppInsightClassicAttributeKeys.OperationType,
                     AppInsightClassicAttributeKeys.ResponseContentLength,
                     AppInsightClassicAttributeKeys.RequestContentLength,
                     AppInsightClassicAttributeKeys.ItemCount
                };

                foreach (KeyValuePair<string, object> actualTag in activity.TagObjects)
                {
                    Assert.IsTrue(expectedTags.Contains(actualTag.Key), $"{actualTag.Key} is not allowed for {activity.OperationName}");

                    AssertActivity.AssertDatabaseAndContainerName(activity.OperationName, actualTag);
                }

                object statusCodeFromTraces = activity.GetTagItem("db.response.status_code") ?? 
                                                activity.GetTagItem(AppInsightClassicAttributeKeys.StatusCode);
                HttpStatusCode statusCode = (HttpStatusCode)Convert.ToInt32(statusCodeFromTraces);
                int subStatusCode = Convert.ToInt32(activity.GetTagItem("db.cosmosdb.sub_status_code"));
                if (!DiagnosticsFilterHelper.IsSuccessfulResponse(statusCode, subStatusCode))
                {
                    Assert.AreEqual(ActivityStatusCode.Error, activity.Status);
                }
            }
        }

        public static void AreEqualAcrossListeners()
        {
            Assert.AreEqual(
                JsonConvert.SerializeObject(CustomListener.CollectedOperationActivities.OrderBy(x => x.Id)),
                JsonConvert.SerializeObject(CustomOtelExporter.CollectedActivities
                    .Where(activity => activity.OperationName.StartsWith("Operation."))
                    .OrderBy(x => x.Id)));
        }

        private static void AssertDatabaseAndContainerName(string name, KeyValuePair<string, object> tag)
        {
            IList<string> exceptionsForContainerAttribute = new List<string>
            {
                "Operation.CreateDatabaseAsync",
                "Operation.CreateDatabaseIfNotExistsAsync",
                "Operation.ReadAsync",
                "Operation.DeleteAsync",
                "Operation.DeleteStreamAsync"
            };
            
            if ((tag.Key == OpenTelemetryAttributeKeys.ContainerName && !exceptionsForContainerAttribute.Contains(name)) ||
                 (tag.Key == OpenTelemetryAttributeKeys.DbName))
            {
                Assert.IsNotNull(tag.Value, $"{tag.Key} is 'null' for {name} operation");
            }
            else if (tag.Key == OpenTelemetryAttributeKeys.ContainerName && exceptionsForContainerAttribute.Contains(name))
            {
                Assert.IsNull(tag.Value, $"{tag.Key} is '{tag.Value}' for {name} operation");
            }
        }

    }
}
