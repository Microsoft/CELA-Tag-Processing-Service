using CELA_Knowledge_Management_Data_Services.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CELA_Knowledge_Management_Data_Services.BusinessLogic
{
    public class AzureTableTagQuery
    {
        public AzureTableTagQuery()
        {

        }


        public async Task<Dictionary<string, int>> GetMostUsedTagsAsync(CloudTable TagTable)
        {
            Dictionary<string, int> tagDictionary = new Dictionary<string, int>();
            var taggedCommunications = await GetTaggedCommunicationsAsync(TagTable);
            foreach (var taggedCommunication in taggedCommunications)
            {
                var tagCluster = taggedCommunication.EmailTagCluster;
                if (tagCluster != null && tagCluster.Trim().Length > 0)
                {
                    var tags = tagCluster.Split(' ');
                    foreach (var tag in tags)
                    {
                        // Do not process empty tags
                        if (tag.Trim().Length > 0)
                        {
                            if (tagDictionary.ContainsKey(tag))
                            {
                                tagDictionary[tag] = tagDictionary[tag] + 1;
                            }
                            else
                            {
                                tagDictionary[tag] = 1;
                            }
                        }
                    }
                }
            }
            return tagDictionary;
        }

        public async Task<List<EmailSearch>> GetTaggedCommunicationsAsync(CloudTable TagTable)
        {
            return await GetTaggedCommunicationsAsync(TagTable, null);
        }


        public async Task<List<EmailSearch>> GetTaggedCommunicationsAsync(CloudTable TagTable, string PartitionKey)
        {
            List<EmailSearch> taggedCommunications = new List<EmailSearch>();
            try
            {
                if (TagTable != null)
                {
                    TableQuery<TagsSearchByTagStartTokenOrdinal> rangeQuery = null;
                    if (PartitionKey != null && PartitionKey.Length > 0)
                    {
                        rangeQuery = new TableQuery<TagsSearchByTagStartTokenOrdinal>().Where(
                        TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, PartitionKey));
                    }
                    else
                    {
                        rangeQuery = new TableQuery<TagsSearchByTagStartTokenOrdinal>();
                    }

                    TableContinuationToken tableContinuationToken = null;
                    do
                    {
                        var queryResponse = await TagTable.ExecuteQuerySegmentedAsync(rangeQuery, tableContinuationToken);
                        tableContinuationToken = queryResponse.ContinuationToken;
                        foreach (var item in queryResponse.Results)
                        {

                            taggedCommunications.Add(item);
                        }
                    }
                    while (tableContinuationToken != null);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return taggedCommunications;
        }

        public CloudStorageAccount GetStorageAccount()
        {
            CloudStorageAccount storageAccount = null;
            try
            {
                storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=[TABLE_NAME];AccountKey=[STORAGE_KEY];EndpointSuffix=core.windows.net");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return storageAccount;
        }
    }
}
