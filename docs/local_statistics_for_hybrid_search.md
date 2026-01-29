# Enabling users to choose global vs local/focused statistics for FullTextScore 

## Why? 

Cosmos DB’s implementation of FullTextScore computes BM25 statistics (term frequency, inverse document frequency, and document length) across all documents in the container, including all physical and logical partitions. 

While this provides a valid and comprehensive representation of statistics for the entire dataset, it introduces challenges for several common use cases. 

In multi-tenant scenarios, it is often necessary to isolate queries to data belonging to a specific tenant, typically defined by the partition key or a component of a hierarchical partition key. This enables scoring to reflect statistics that are accurate for that tenant’s dataset, rather than for the entire container. For customers such as Veeam and Sitecore, which operate large multi-tenant containers, this is not just an optimization but a requirement. Their tenants often operate in very different domains, which can significantly change the distribution and importance of keywords and phrases. Using global statistics in these cases leads to distorted relevance rankings. 

In other scenarios involving hundreds or thousands of physical partitions, computing statistics across the entire container can become both time-consuming and expensive. Customers may prefer to use statistics derived from only a subset of partitions to improve performance and reduce RU consumption. Indeed, there is precedence for this as  Azure AI Search defaults to this “local” method.  

## What? 

We propose extending the flexibility of BM25 scoring in Cosmos DB so that developers can choose between a *global FullTextScore* (existing behavior) or *Scoped FullTextScore* (statistics computed restricted to the partition key(s) used in the query). The key aspects: 

For _*global*_ BM25, FullTextScore retains its existing behavior and computes BM25 statistics, such as IDF and average document length, across all documents in the container regardless of any partition key filters in the query. In _*scoped*_ BM25, when a query includes a partition key filter or explicitly requests scoped scoring, the engine computes these statistics only over the subset of documents within the specified partition key values. Query results are still returned only from the filtered partitions, and the resulting scores and ranking reflect relevance within that partition-specific slice of data.

## How?

The user issues query like: 

```
SELECT TOP 10 * FROM c   
WHERE c.tenantId = @tenantId   
ORDER BY RANK FullTextScore(c.text, "keywords") 
```
 

And sets a new [QueryRequestOption](../Microsoft.Azure.Cosmos/src/RequestOptions/QueryRequestOptions.cs) called `FullTextScoreScope` which can be set to one of two values: `local` or `global`. The request option is inspected, and the query uses scoped/full stats accordingly. 

### Pros: 

- No change required to SQL syntax or function call, transparent to existing queries (if default global). 
- Good separation of concerns between ranking logic and query syntax; developer chooses via configuration rather than code change. 

### Cons: 

- Requires SDK/API  layer change and tooling to support the new request parameter, less discoverable in SQL alone. 
- Harder for query authoring (especially interactive) to know which mode is used, less transparent in the query text. 
- Might limit granularity: if query has multiple FullTextScore calls maybe you cannot mix scope modes (depending on implementation), less flexible than in-call parameter.


## Implementation Plan

Create a new member in [QueryRequestOptions.cs](../Microsoft.Azure.Cosmos/src/RequestOptions/QueryRequestOptions.cs) called `FullTextScoreScope` which should be an enum with two possible values: `Local` or `Global`. The default value in `QueryRequestOptions` should be `Global`

The `FullTextScoreScope` from the `QueryRequestOptions` needs to be plumbed to `HybridSearchCrossPartitionQueryPipelineStage.MonadicCreate` defined in [HybridSearchCrossPartitionQueryPipelineStage.cs](../Microsoft.Azure.Cosmos/src/Query/Core/Pipeline/CrossPartition/HybridSearch/HybridSearchCrossPartitionQueryPipelineStage.cs). In the `HybridSearchCrossPartitionQueryPipelineStage` constructor when creating the `tryCatchGlobalStatisticsPipeline`, if the `FullTextScoreScope` is set to `Global` then `allRanges` is passed in to the `targetRanges` parameter else the `targetRanges` are passed in.