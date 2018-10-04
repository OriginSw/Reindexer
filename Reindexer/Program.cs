using Elasticsearch.Net;
using log4net;
using Nest;
using Newtonsoft.Json;
using Reindexer.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Reindexer
{
    public class Program
    {
        private static readonly ILog log = LogManager.GetLogger("Reindexer");

        private static void Main(string[] args)
        {
            var clientFrom = GetClient(ConfigSection.Default.Node.From, ConfigSection.Default.Index.From);
            var clientTo = GetClient(ConfigSection.Default.Node.To, ConfigSection.Default.Index.To);

            Reindex(clientFrom, clientTo);
            //FindId(localClient);
            //Search(localClient);
            //IndexDocument();
            //Bulk();
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
                //.DisableDirectStreaming()//For debug
                .DefaultTypeNameInferrer(q => q.Name);

            if (connectionObject.ContainsKey("User") && connectionObject.ContainsKey("Password"))
                settings = settings.BasicAuthentication(connectionObject["User"], connectionObject["Password"]);

            var client = new ElasticClient(settings);

            return client;
        }

        private static void FindId(IElasticClient client)
        {
            var input = Console.ReadLine();
            log.Info(input);

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

        //private static void Search(IElasticClient client)
        //{
        //    var dateFrom = DateMath.FromString("05/11/2016 00:00");
        //    var dateTo = DateMath.FromString("11/02/2017 15:29");

        //    var searchResult = client.Search<EmailLog>(s => s
        //            //.From(0)
        //            //.Size(100)
        //            .Query(q => q.DateRange(x => x.Field(f => f.RequestedOn).LessThanOrEquals(dateTo).Format("dd/MM/yyyy HH:mm")
        //                                                                    .GreaterThanOrEquals(dateFrom).Format("dd/MM/yyyy HH:mm")))
        //            .SearchType(SearchType.Scan)
        //            .Scroll("2m"));

        //    var result = searchResult;
        //    var scrollRequest = new ScrollRequest(result.ScrollId, "2m");
        //    searchResult = client.Scroll<EmailLog>(scrollRequest);
        //    var total = searchResult.Total;

        //    Console.WriteLine("Total: " + total);
        //    if (searchResult.Documents.Any())
        //    {
        //        var first = searchResult.Documents.First();
        //        var last = searchResult.Documents.Last();
        //        Console.WriteLine("First: " + JsonConvert.SerializeObject(first));
        //        Console.WriteLine("Last: " + JsonConvert.SerializeObject(last));
        //    }

        //    Console.WriteLine("Presione una tecla");
        //    Console.ReadKey();
        //}

        public static void Reindex(IElasticClient clientFrom, IElasticClient clientTo)
        {
            var dateFrom = DateMath.FromString(ConfigSection.Default.Date.From);
            var dateTo = DateMath.FromString(ConfigSection.Default.Date.To);

            log.Info("Reindexing documents to new index...");
            var searchResult = clientFrom.Search<EmailLog>(s => s
               .From(0)
               .Size(ConfigSection.Default.Bulk.Size)
               .Query(q =>
                   q.DateRange(dr =>
                       dr.Field(f => f.RequestedOn)
                       .Format("yyyy-MM-dd HH:mm:ss")
                       .GreaterThanOrEquals(dateFrom)
                       .LessThanOrEquals(dateTo)
                   )
                   ||
                   q.Nested(n =>
                       n.Path("Events")
                       .ScoreMode(NestedScoreMode.None)
                       .Query(nq =>
                           nq.DateRange(dr =>
                               dr.Field("Events.CreatedOn")
                               .Format("yyyy-MM-dd HH:mm:ss")
                               .GreaterThanOrEquals(dateFrom)
                               .LessThanOrEquals(dateTo)
                           )
                       )
                   )
               )
               .SearchType(SearchType.QueryThenFetch)
               .Scroll(ConfigSection.Default.Bulk.Scroll));

            if (!searchResult.IsValid)
            {
                log.Error(searchResult.OriginalException);
                throw new Exception("Request invalid.");
            }

            long total = searchResult.Total;

            if (total <= 0)
                log.Info("Existing index has no documents, nothing to reindex.");
            else
            {
                try
                {
                    var page = 0;
                    IBulkResponse response = null;
                    var indexedDocuments = 0;
                    do
                    {
                        var result = searchResult;
                        var scrollRequest = new ScrollRequest(result.ScrollId, ConfigSection.Default.Bulk.Scroll);

                        try
                        {
                            searchResult = clientFrom.Scroll<EmailLog>(scrollRequest);

                            if (!searchResult.IsValid)
                                throw new Exception("Request invalid.", searchResult.OriginalException);
                        }
                        catch (Exception e)
                        {
                            log.Error("Scroll", e);
                            log.Info("---");
                            log.Info("---");
                            log.Info("---");
                            log.Info("Second try...");

                            Thread.Sleep(5000);// 5 sec
                            searchResult = clientFrom.Scroll<EmailLog>(scrollRequest);

                            if (!searchResult.IsValid)
                                throw new Exception("Request invalid.", searchResult.OriginalException);
                        }

                        if (searchResult.Documents != null && searchResult.Documents.Any())
                        {
                            response = clientTo.Bulk(bulk =>
                            {
                                foreach (var hit in searchResult.Hits)
                                    bulk.Index<EmailLog>(x => x.Document(hit.Source).Id(hit.Id));
                                return bulk;
                            });
                            indexedDocuments += response.Items.Count();
                            log.Info("Reindexing percentage: " + ((indexedDocuments * 100) / total));
                        }
                        ++page;
                    }
                    while (searchResult.IsValid && response != null && response.IsValid && searchResult.Documents != null && searchResult.Documents.Any());

                    log.Info("Reindexing complete!");
                    Console.Read();
                }
                catch (Exception e)
                {
                    log.Error(e);
                    Console.ReadKey();
                }
            }
        }

        //public static void Bulk()
        //{
        //    var client = CreateClientForSync("http://origin-8:9200", 10000);

        //    var fruta = client.Bulk<dynamic>("sqlserver_es_sync", asdasd.GetPartialIndexBulk("log_pt_dataSources", new
        //    {
        //        success = true
        //    }));

        //    Console.WriteLine(JsonConvert.SerializeObject(fruta.Body));
        //    Console.ReadKey();
        //}

        #region v3

        //public static void IndexDocument()
        //{
        //    var client = CreateClientForSync("https://search-origin-servicev3-wcltfvkboi2dps46auyzd3ilvu.us-east-1.es.amazonaws.com", 10000);

        //    var response = client.Index<dynamic>("origin_sqlserver_es_sync_20180721001444", "bulk_log", new
        //    {
        //        success = true,
        //        httpStatusCode = 204,
        //        documentsIndexed = 45,
        //        startedOn = DateTime.UtcNow,
        //        duration = 10000 + "ms",
        //        exception = ""
        //    });

        //    Console.WriteLine(JsonConvert.SerializeObject(new Guid(response.Body._id)));
        //    Console.WriteLine();

        //    Console.WriteLine(JsonConvert.SerializeObject(response));
        //    Console.ReadKey();
        //}

        private static ConnectionConfiguration GetConfiguration(string uri)
        {
            IConnectionPool connectionPool;
            //if (ConfigSection.ElasticConnection.Nodes.AllowSniffing)
            //    connectionPool = new SniffingConnectionPool(ConfigSection.ElasticConnection.Uris);
            //else
            var uris = new List<Uri>();
            uris.Add(new Uri(uri));
            connectionPool = new StaticConnectionPool(uris);
            var connectionConfig = new ConnectionConfiguration(connectionPool);

            //var user = ConfigSection.ElasticConnection.Auth.User;
            //var pass = ConfigSection.ElasticConnection.Auth.Password;
            //if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
            //    connectionConfig = connectionConfig.BasicAuthentication(user, pass);

            return connectionConfig;
        }

        public static ConnectionConfiguration GetConfigurationForSync(string uri, double timeout)
        {
            var config = GetConfiguration(uri);
            config.RequestTimeout(TimeSpan.FromMilliseconds(timeout));
            return config;
        }

        public static ElasticLowLevelClient CreateClientForSync(string uri, double timeout)
        {
            var config = GetConfigurationForSync(uri, timeout);

            return new ElasticLowLevelClient(config);
        }

        #endregion v3
    }

    //public static class asdasd
    //{
    //    public static string GetPartialIndexBulk(string type, object value)
    //    {
    //        return string.Format("{0}\n{1}\n",
    //            JsonConvert.SerializeObject(new { index = new { _type = type } }, Formatting.None),
    //            JsonConvert.SerializeObject(value, Formatting.None));
    //    }
    //}
}