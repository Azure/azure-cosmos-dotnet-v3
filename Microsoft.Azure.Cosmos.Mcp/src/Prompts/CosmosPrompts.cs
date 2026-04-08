// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Prompts
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using Microsoft.Extensions.AI;
    using ModelContextProtocol.Server;

    /// <summary>
    /// MCP prompts for guided data exploration, query optimization, and data modeling.
    /// </summary>
    [McpServerPromptType]
    public class CosmosPrompts
    {
        [McpServerPrompt(Name = "explore_data"), Description("Guided data exploration workflow. Instructs the agent to discover schema, analyze data distribution, and report findings.")]
        public static IEnumerable<ChatMessage> ExploreData(
            [Description("Target database name")] string database,
            [Description("Target container name")] string container,
            [Description("What you want to learn about your data")] string goal)
        {
            return new[]
            {
                new ChatMessage(ChatRole.User,
                    $@"I want to explore data in the Cosmos DB container '{container}' in database '{database}'.

My goal: {goal}

Please follow these steps:
1. Use the cosmos_get_schema tool to discover the document schema for '{database}/{container}'
2. Read the indexing policy resource at cosmos://{database}/{container}/indexing-policy
3. Run a sample query like SELECT TOP 5 * FROM c to see actual data
4. Analyze partition key cardinality with a query like SELECT c.<partitionKeyField>, COUNT(1) as cnt FROM c GROUP BY c.<partitionKeyField>
5. Based on all the above, report your findings relevant to my goal: {goal}

Be specific about field names, data types, and any patterns you discover.")
            };
        }

        [McpServerPrompt(Name = "optimize_query"), Description("Query optimization workflow. Analyzes a query's performance and suggests improvements.")]
        public static IEnumerable<ChatMessage> OptimizeQuery(
            [Description("Target database name")] string database,
            [Description("Target container name")] string container,
            [Description("The SQL query to optimize")] string query)
        {
            return new[]
            {
                new ChatMessage(ChatRole.User,
                    $@"I need help optimizing this Cosmos DB query for container '{container}' in database '{database}':

```sql
{query}
```

Please follow these optimization workflow:
1. Read the indexing policy at cosmos://{database}/{container}/indexing-policy
2. Read the container schema at cosmos://{database}/{container}/schema
3. Run the query using cosmos_query and note the RU cost and latency
4. Check if the query is cross-partition (look at diagnostics)
5. Analyze whether the query filters align with indexed paths
6. Suggest specific improvements:
   - Index additions that could help
   - Query rewrites for better performance
   - Whether a composite index would help for ORDER BY
   - Partition key scoping if applicable
7. Run the optimized version and compare the RU cost

Show the before/after RU cost comparison.")
            };
        }

        [McpServerPrompt(Name = "model_data"), Description("Data modeling assistant for new application scenarios. Recommends document structure, partition key, and indexing.")]
        public static IEnumerable<ChatMessage> ModelData(
            [Description("Description of the application scenario")] string scenario,
            [Description("How the data will be queried (access patterns)")] string access_patterns)
        {
            return new[]
            {
                new ChatMessage(ChatRole.User,
                    $@"I need help designing a Cosmos DB data model for this scenario:

**Application:** {scenario}

**Access Patterns:** {access_patterns}

Please analyze and recommend:
1. **Document structure**: Should data be embedded in a single document or split across collections? Consider the access patterns.
2. **Partition key**: What should the partition key be? Consider cardinality, even distribution, and query alignment.
3. **Indexing policy**: What paths should be indexed vs excluded? Any composite indexes needed?
4. **Estimated RU costs**: For each access pattern, estimate the approximate RU cost.
5. **Trade-offs**: What are the pros/cons of your recommended approach?

If any existing databases/containers are available, check them with cosmos_list_databases and cosmos_list_containers for reference.
Provide concrete JSON examples of the document structure you recommend.")
            };
        }
    }
}
