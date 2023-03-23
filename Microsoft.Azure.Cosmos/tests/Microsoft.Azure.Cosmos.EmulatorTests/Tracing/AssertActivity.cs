//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    internal static class AssertActivity
    {
        public static void IsValid(Activity activity)
        {
            Assert.IsTrue(activity.OperationName == activity.DisplayName);

            Assert.IsFalse(string.IsNullOrEmpty(activity.GetTagItem("db.cosmosdb.connection_mode").ToString()), $"connection mode is emtpy for {activity.OperationName}");

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
                 "kind",
                 "db.system",
                 "db.name",
                 "db.operation",
                 "net.peer.name",
                 "db.cosmosdb.client_id",
                 "db.cosmosdb.machine_id",
                 "db.cosmosdb.user_agent",
                 "db.cosmosdb.connection_mode",
                 "db.cosmosdb.operation_type",
                 "db.cosmosdb.container",
                 "db.cosmosdb.request_content_length_bytes",
                 "db.cosmosdb.response_content_length_bytes",
                 "db.cosmosdb.status_code",
                 "db.cosmosdb.sub_status_code",
                 "db.cosmosdb.request_charge",
                 "db.cosmosdb.regions_contacted",
                 "db.cosmosdb.retry_count",
                 "db.cosmosdb.item_count",
                 "db.cosmosdb.request_diagnostics",
                 "exception.type",
                 "exception.message",
                 "exception.stacktrace",
                 "db.cosmosdb.activity_id",
                 "db.cosmosdb.correlated_activity_id"
            };

            foreach (KeyValuePair<string, string> actualTag in activity.Tags)
            {
                Assert.IsTrue(expectedTags.Contains(actualTag.Key), $"{actualTag.Key} is not allowed for {activity.OperationName}");

                AssertActivity.AssertDatabaseAndContainerName(activity.OperationName, actualTag);
            }
        }

        public static void AreEqualAcrossListeners()
        {
            Assert.AreEqual(
                JsonConvert.SerializeObject(CustomListener.CollectedActivities.OrderBy(x => x.Id)),
                JsonConvert.SerializeObject(CustomOtelExporter.CollectedActivities.OrderBy(x => x.Id)));
        }

        private static void AssertDatabaseAndContainerName(string name, KeyValuePair<string, string> tag)
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
