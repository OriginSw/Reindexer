using Elasticsearch.Net;
using Nest;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reindexer
{
    public class Program
    {
        private static void Main(string[] args)
        {
            var localClient = GetClient("Nodes=http://local-elasticsearch:9200", "email-service-prod-04nov2016");
            var localClient2 = GetClient("Nodes=http://local-elasticsearch:9200", "email-service-prod-04nov2016-copy");
            var remoteClient = GetClient("Nodes=http://logs.foxites.com", "email-service");

            Reindex(localClient, localClient2);
            //FindId(localClient);
            //Search(localClient);
        }

        private static IElasticClient GetClient(string connectionString, string indexName)
        {
            Dictionary<string, string> connectionObject = connectionString
                .Split(';')
                .ToDictionary(x => x.Split('=')[0].Trim(), x => x.Split('=')[1].Trim());

            var nodes = connectionObject["Nodes"].Split(',').Select(x => new Uri(x));
            var pool = new StaticConnectionPool(nodes);

            var settings = new ConnectionSettings(pool)
                .DefaultIndex(indexName)
                .DefaultFieldNameInferrer(s => s)
                .DefaultTypeNameInferrer(q => q.Name);

            if (connectionObject.ContainsKey("User") && connectionObject.ContainsKey("Password"))
                settings = settings.BasicAuthentication(connectionObject["User"], connectionObject["Password"]);

            var client = new ElasticClient(settings);

            return client;
        }

        private static void FindId(IElasticClient client)
        {
            var input = Console.ReadLine();
            Console.WriteLine(input);

            var result = client.Get<EmailLog>(input);

            if (result.Found)
            {
                var log = result.Source;
                var serializedLog = JsonConvert.SerializeObject(log);
                Console.WriteLine(serializedLog);
            }
            else
                Console.WriteLine("Not Found");
            Console.Read();
        }

        private static void Search(IElasticClient client)
        {
            var dateFrom = DateMath.FromString("05/11/2016 00:00");
            var dateTo = DateMath.FromString("11/02/2017 15:29");

            var searchResult = client.Search<EmailLog>(s => s
                    //.From(0)
                    //.Size(100)
                    .Query(q => q.DateRange(x => x.Field(f => f.RequestedOn).LessThanOrEquals(dateTo).Format("dd/MM/yyyy HH:mm")
                                                                            .GreaterThanOrEquals(dateFrom).Format("dd/MM/yyyy HH:mm")))
                    .SearchType(SearchType.Scan)
                    .Scroll("2m"));

            var result = searchResult;
            var scrollRequest = new ScrollRequest(result.ScrollId, "2m");
            searchResult = client.Scroll<EmailLog>(scrollRequest);
            var total = searchResult.Total;

            Console.WriteLine("Total: " + total);
            if (searchResult.Documents.Any())
            {
                var first = searchResult.Documents.First();
                var last = searchResult.Documents.Last();
                Console.WriteLine("First: " + JsonConvert.SerializeObject(first));
                Console.WriteLine("Last: " + JsonConvert.SerializeObject(last));
            }

            Console.WriteLine("Presione una tecla");
            Console.ReadKey();
        }

        public static void Reindex(IElasticClient clientFrom, IElasticClient clientTo)
        {
            var dateFrom = DateMath.FromString("05/11/2016 00:00");
            var dateTo = DateMath.FromString("11/02/2017 15:29");

            Console.WriteLine("Reindexing documents to new index...");
            var searchResult = clientFrom.Search<EmailLog>(s => s
                .From(0)
                .Size(100)
                .Query(q => q.DateRange(x => x.Field(f => f.RequestedOn).LessThanOrEquals(dateTo).Format("dd/MM/yyyy HH:mm")
                                                                        .GreaterThanOrEquals(dateFrom).Format("dd/MM/yyyy HH:mm")))
                //.Query(q => q.MatchAll())
                .SearchType(SearchType.Scan)
                .Scroll("2m"));

            long total = searchResult.Total;

            if (total <= 0)
                Console.WriteLine("Existing index has no documents, nothing to reindex.");
            else
            {
                var page = 0;
                IBulkResponse response = null;
                var indexedDocuments = 0;
                do
                {
                    var result = searchResult;
                    var scrollRequest = new ScrollRequest(result.ScrollId, "2m");
                    searchResult = clientFrom.Scroll<EmailLog>(scrollRequest);

                    if (searchResult.Documents != null && searchResult.Documents.Any())
                    {
                        response = clientTo.Bulk(bulk =>
                        {
                            foreach (var hit in searchResult.Hits)
                                bulk.Index<EmailLog>(bi => bi.Document(hit.Source).Id(hit.Id));
                            return bulk;
                        });
                        indexedDocuments += response.Items.Count();
                        Console.WriteLine("Reindexing percentage: " + ((indexedDocuments * 100) / total));
                    }
                    ++page;
                }
                while (searchResult.IsValid && response != null && response.IsValid && searchResult.Documents != null && searchResult.Documents.Any());

                Console.WriteLine("Reindexing complete!");
                Console.Read();
            }
        }
    }
}