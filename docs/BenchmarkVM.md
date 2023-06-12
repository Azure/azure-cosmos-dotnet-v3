Design aproach to run Benchmark tool on VMs
* [Backgraound.](#backgraound)
* [Proposed Solution.](#proposed-solution)

## Backgraund
The goal of this feature is to make the process of using the CosmosDB benchmarking tools as easy as possible for our customers. Here we are looking to use ARM templates to deploy and run our benchmarking.
 Here we will switch to a VM based model which aims to simplify the process and reduce costs. Here we will also add more features:

- Ability to execute benchmarking on multiple machines with one ARM Template     
- Include more input parameters for testing features such as - ClientTelemetry and DistributedTracing
- Include extensive logs with full diagnostics
- AppInsights: Live metrics (10s)
    - Success rate
    - Error
    - P90, P99, P999, P9999


## Proposed Solution
- .NET SDK Benchmark Tool will run on multiple VMs in parallel
    - To track the state of each benchmark execution, we need to store the start and finish statuses for all running benchmarks. For this, we can use a Result Container in CosmosDB and create items for each VM with the status. After the benchmark is completed, the statuses will be changed to "COMPLETE" state.

- While benchark is running capture each request each exceeds specified by input parameter threshold and collect them in file on disk using [BenchmarkLatencyEventSource](https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos.Samples/Tools/Benchmark/BenchmarkLatencyEventSource.cs) 
    - Output requests to diagnostics file.
        - Use CSV format with a pipe-separated format. The data to be stored may include latency and diagnostic information in JSON format.
        - Create a background task to check file size and available disk space, and generate alerts if there is insufficient free space.
    - BLob Storage Account.
        - Alternatively, we can send each request's diagnostic log immediately to the Blob Storage Account.  
- During benchmark is running collect App Insights.
    - Collect app insights with 10 seconds granulation.
    - Using [App.Metrics](https://www.app-metrics.io/reporting/reporters/app-insights/) store them in Azure Application Insighs. 
- When benchmark on VM is completed save output logs and captured diagnostics to blob storate.
    - Blob Storage creation
        - Specify instruction of how to create Azure Blob storage
        - Add to ARM template Blob Storage Account creation
    - Develop the feature in .NET SDK Benchmark Tool that saves files in blob storage after benchmark complete

 

```mermaid
sequenceDiagram
    participant A as Azure VM <br> [runs benchmark tool]
    participant B as CosmosDB <br> [ store VMs benchmark status]
    participant C as AppInsights <br> [Store and Visualize AppInisghts]
    participant D as Blob Storage<br> [Blob Storage Account for diagnostics and logs]

    loop For each VM
        A ->> B: Set start status for each VM
        A ->> B: Execute benchmark
        B -->> A: Update status when VM finishes

    A ->> B: Capture requests exceeding threshold using BenchmarkLatencyEventSource
    B -->> D: Save request diagnostics file (CSV with pipe-separated format)
    B -->> D: Save output logs and captured diagnostics to Blob Storage Account

    A ->> C: Collect App Insights metrics every 10 seconds

    A ->> D: Save benchmark output and diagnostics logs to Blob Storage Account

    Note over A, D: Benchmark completed
    end

    A -->> B: Status update to "COMPLETE" for all VMs
```

```mermaid
sequenceDiagram
    participant A as ARM Template <br> [Input parameters]
    participant B as Azure VM <br> [runs benchmark tool]
    participant C as CosmosDB <br> [ Store VMs benchmark status]
    participant D as AppInsights <br> [Store and Visualize AppInisghts]
    participant E as Blob Storage <br> [Blob Storage Account for diagnostics and logs]

    A->>B: Run .NET SDK Benchmark Tool
    loop Benchmark Execution on each VM
        loop every 10 seconds

            B->>C: Status update to "RUNNING"
            C->>B: Capture request diagnostics exceeding threshold
            B->>D: Collect App Insights data
        end
        B->>C: Status update to "COMPLETE"
    end
    B->>E: Save output logs and diagnostics to Blob Storage

```



```mermaid
flowchart TD
    A0([ARM<br>Create Blob <br> storage account])
    A1([ARM<br>Create AppInsights <br>account])
    A([ARM <br> Start Benchmark])
    C[(CosmosDB)]
    D[(AppInsights)]
    E[(Blob Storage)]
    
    B[Azure VM 0]
    B1[Azure VM 1]
    B2[Azure VM n]
    
    A0 --> A
    A1 --> A
    A --> VM
    subgraph VM [Virtual Machines]
    B ~~~ B1 ~~~~ B2
    end
    VM --Execute Benchmark<br>START/COMPLETE status--> C
    VM --Store Metrics--> D
    VM --Store Output logs<br>Diagnostic data--> E
```