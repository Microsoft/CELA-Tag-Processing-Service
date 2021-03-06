using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CELA_Knowledge_Management_Data_Services.BusinessLogic;
using CELA_Knowledge_Management_Data_Services.Models;
//using Microsoft.Azure.Storage;
//using Microsoft.Azure.Storage.Auth;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;

namespace CELA_Tags_Parsing_Service.Storage
{
    public class StorageUtility
    {
        private static StorageUtility storageUtility;
        private CloudStorageAccount storageAccount;
        private CloudTableClient tableClient;
        private CloudTable emailTable;
        private string graphDBHostName;
        private int graphDBPort = -1;
        private string graphDBDatabaseName;
        private string graphDBCollectionName;
        private string graphDBAccessKey;

        private StorageUtility(string StorageAccountName, string StorageAccountKey, string StorageAccountTableName, string GraphDBHostName, string GraphDBPort, string GraphDBDatabaseName, string GraphDBCollectionName, string GraphDBAccessKey)
        {
            storageAccount = new CloudStorageAccount(new StorageCredentials(StorageAccountName, StorageAccountKey), true);
            tableClient = storageAccount.CreateCloudTableClient();
            emailTable = tableClient.GetTableReference(StorageAccountTableName);

            graphDBHostName = GraphDBHostName;
            int.TryParse(GraphDBPort, out graphDBPort);
            graphDBDatabaseName = GraphDBDatabaseName;
            graphDBCollectionName = GraphDBCollectionName;
            graphDBAccessKey = GraphDBAccessKey;
        }

        /// <summary>Gets the StorageUtility instance using a Singleton constructor.</summary>
        /// <returns>A StorageUtility instance.</returns>
        public static StorageUtility GetInstance()
        {
            if (storageUtility is null)
            {                              
                storageUtility = new StorageUtility(Properties.Resources.TagulousAzureTableResourceString, Properties.Resources.TagulousAzureTableAccessKey, Properties.Resources.TagulousAzureTableName, Properties.Resources.TagulousGraphDBHostName, Properties.Resources.TagulousGraphDBPort, Properties.Resources.TagulousGraphDBDatabase, Properties.Resources.TagulousGraphDBCollection, Properties.Resources.TagulousGraphDBAccessKey);
            }
            return storageUtility;
        }

        /// <summary>Stores the email to Azure Table.</summary>
        /// <param name="email">The processed EmailSearch (or subclass) object to store.</param>
        public void StoreEmailToTableStorage(EmailSearch email)
        {
            if (string.IsNullOrEmpty(email.PartitionKey))
            {
                //Set the partition key based on cascading priority rules
                if (!string.IsNullOrEmpty(email.EmailSenderTeam))
                {
                    email.PartitionKey = email.EmailSenderTeam;
                }
                else if (!string.IsNullOrEmpty(email.EmailSenderGroup))
                {
                    email.PartitionKey = email.EmailSenderGroup;
                }
                else if (!string.IsNullOrEmpty(email.EmailSender))
                {
                    email.PartitionKey = email.EmailSender;
                }
            }

            if (string.IsNullOrEmpty(email.RowKey))
            {
                email.RowKey = email.EmailSentTime;
            }

            // Create the TableOperation that inserts the customer entity.
            TableOperation insertOperation = TableOperation.Insert(email);
            // Execute the insert operation.
            var result = emailTable.Execute(insertOperation);
        }

        public void StoreEmailToGraphStorage(EmailSearch communication)
        {
            var communicationProcessor = CommunicationProcessingBusinessLogic.ProcessCommunicationToGraphDB(communication, graphDBHostName, graphDBPort, graphDBAccessKey, graphDBDatabaseName, graphDBCollectionName);

        }

        public void StoreDocumentToGraphStorage(Document document)
        {
            var communicationProcessor = CommunicationProcessingBusinessLogic.ProcessDocumentToGraphDB(document, graphDBHostName, graphDBPort, graphDBAccessKey, graphDBDatabaseName, graphDBCollectionName);
        }

        public string GetHostname()
        {
            return graphDBHostName;
        }
    }
}
