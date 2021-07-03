using Relativity.Services.Objects;
using Relativity.Services.Objects.DataContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KCD_1042192.Utility
{
    public class QuerySubset
    {
        /// <summary>
        /// Publically accessible method that uses the yield keyword so that not only queries are
        /// batched but also batch the returned results to the caller
        /// </summary>
        /// <typeparam name="T"> A DTO with Artifact as the base </typeparam>
        /// <param name="proxy">Proxy interface for the Object Manager service</param>
        /// <param name="originalQuery">
        /// A preconstructed Query that would usually be sent to the DTO repository
        /// </param>
        /// <param name="batchSize">
        /// The maximum batch size of the full artifacts(with fields) requested from the server
        /// </param>
        /// <returns> IEnumerable list or RDOs </returns>
        public static IEnumerable<RelativityObject> PerformQuerySubset(IObjectManager proxy, int workspaceId, QueryRequest originalQuery, Int32 batchSize)
        {
            var artifactIds = GetQueryArtifactIds(proxy, workspaceId, originalQuery, batchSize).ToList();
            var currentBatchIds = new List<Int32>();
            var totalCount = 0;
            foreach (var id in artifactIds)
            {
                currentBatchIds.Add(id);
                totalCount++;
                if (totalCount % batchSize == 0 || totalCount == artifactIds.Count())
                {
                    var currentBatchArtifacts = RetrieveBatch(proxy, workspaceId, originalQuery, currentBatchIds);
                    foreach (var individualResult in currentBatchArtifacts.Objects)
                    {
                        yield return individualResult;
                    }
                    currentBatchIds.Clear();
                }
            }
        }

        /// <summary>
        /// requests full artifacts(with selected fields) from the server
        /// </summary>
        /// <typeparam name="T"> A DTO with Artifact as the base </typeparam>
        /// <param name="proxy"> The Object Manager proxy </param>
        /// <param name="originalQuery"> The developer's original query </param>
        /// <param name="artifactIdBatch"> The artifactIds of the DTOs to return </param>
        /// <returns> Query Result Set of type RDO </returns>
        private static QueryResult RetrieveBatch(IObjectManager proxy, int workspaceId, QueryRequest originalQuery, IEnumerable<Int32> artifactIdBatch)
        {
            var csvArtifactIds = string.Join(",", artifactIdBatch);
            var batchQuery = new QueryRequest
            {
                ObjectType = originalQuery.ObjectType,
                Fields = originalQuery.Fields,
                Condition = $"('Artifact ID' IN ({csvArtifactIds}))",
                Sorts = originalQuery.Sorts,
                RelationalField = originalQuery.RelationalField
            };

            var results = QueryRelativity(proxy, workspaceId, batchQuery, artifactIdBatch.Count());

            return results;
        }

        /// <summary>
        /// All queries are completed with QuerySubset whether the DTO supports it or not so that
        /// this utitlity method can be used with all DTOs
        /// </summary>
        /// <typeparam name="T"> DTO </typeparam>
        /// <param name="proxy"> Object Manager proxy </param>
        /// <param name="query"> Query to Execute </param>
        /// <param name="batchSize"> maximum number of items to return in a single call </param>
        /// <returns> traditional Query Result Set </returns>
        private static QueryResult QueryRelativity(IObjectManager proxy, int workspaceId, QueryRequest query, Int32 batchSize)
        {
            var nextStart = 1;
            var retValue = new QueryResult();

            var queryResults = Task.Run(() => proxy.QueryAsync(workspaceId, query, nextStart, batchSize)).GetAwaiter().GetResult();
            CumulateQueryResults(queryResults, retValue);

            if (queryResults.Objects.Count > 0)
            {
                var batchAvailable = nextStart < queryResults.TotalCount;
                while (batchAvailable)
                {
                    nextStart += queryResults.ResultCount;
                    queryResults = Task.Run(() => proxy.QueryAsync(workspaceId, query, nextStart, batchSize)).GetAwaiter().GetResult();

                    CumulateQueryResults(queryResults, retValue);

                    batchAvailable = nextStart < queryResults.TotalCount;
                }
            }
            return retValue;
        }

        /// <summary>
        /// Combines the multiple query result sets into each other.
        /// </summary>
        /// <typeparam name="T"> DTO type </typeparam>
        /// <param name="newAddition"> recent query result </param>
        /// <param name="cumulativeResults"> Query results where values will cumulate </param>
        private static void CumulateQueryResults(QueryResult newAddition, QueryResult cumulativeResults)
        {
            cumulativeResults.Objects.AddRange(newAddition.Objects);
            cumulativeResults.ResultCount += newAddition.ResultCount;
            cumulativeResults.TotalCount = newAddition.TotalCount;
        }

        /// <summary>
        /// Executes the developer's original query with no fields and only returns the artifactIds
        /// of the results in order to get the results quickly
        /// </summary>
        /// <typeparam name="T"> A DTO with Artifact as the base </typeparam>
        /// <param name="proxy"> Object Manager proxy </param>
        /// <param name="originalQuery"> The developer's original query </param>
        /// <returns> An IEnumerable list of results represented solely by their ArtifactId </returns>
        private static IEnumerable<Int32> GetQueryArtifactIds(IObjectManager proxy, int workspaceId, QueryRequest originalQuery, Int32 batchSize)
        {
            var retVal = new List<Int32>();
            var artifactIdQuery = new QueryRequest
            {
                ObjectType = originalQuery.ObjectType,
                Fields = Array.Empty<FieldRef>(),
                Condition = originalQuery.Condition,
                Sorts = originalQuery.Sorts,
                RelationalField = originalQuery.RelationalField,
            };

            var results = QueryRelativity(proxy, workspaceId, artifactIdQuery, batchSize);

            retVal.AddRange(results.Objects.Select(x => x.ArtifactID).ToList());

            return retVal;
        }
    }
}