//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ApplicationInsights
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;

    public class TelemetryListener : IObserver<KeyValuePair<string, object>>
    {
        private readonly TelemetryClient? telemetryClient;

        public TelemetryListener()
        {
        }

        public TelemetryListener(TelemetryClient client)
        {
            this.telemetryClient = client;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            Console.WriteLine($"{value.Key} => {(CosmosDiagnostics)value.Value}");

          /*  if (value.Key.Contains("Diagnostics"))
            {
                CosmosDiagnostics diagnostics = (CosmosDiagnostics)value.Value;

                //Console.WriteLine(value.Key + " => " + value.Value);

                DependencyTelemetry dependencyTelemetry = new DependencyTelemetry(
                                            dependencyTypeName: "CosmosDB-listener-1",
                                            target: "mytarget",
                                            dependencyName: "mydependencyName",
                                            data: "mydata",
                                            startTime: DateTime.Now,
                                            duration: diagnostics.GetClientElapsedTime(),
                                            resultCode: "200",
                                            true);

                dependencyTelemetry.Properties.Add("Diagnostics", diagnostics.ToString());

                this.telemetryClient.TrackDependency(dependencyTelemetry);
            }

            // before exit, flush the remaining data
            this.telemetryClient.Flush();

            // flush is not blocking when not using InMemoryChannel so wait a bit. There is an active issue regarding the need for `Sleep`/`Delay`
            // which is tracked here: https://github.com/microsoft/ApplicationInsights-dotnet/issues/407
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            Task.Delay(5000).Wait();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits*/
        }
    }
}
