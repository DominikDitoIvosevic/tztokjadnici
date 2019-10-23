using Abot2.Core;
using Abot2.Crawler;
using Abot2.Poco;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tz2
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .CreateLogger();

            Log.Logger.Information("Demo starting up!");

            await DemoSimpleCrawler();
            await DemoSinglePageRequest();
        }

        private static async Task DemoSimpleCrawler()
        {
            var config = new CrawlConfiguration
            {
                MaxPagesToCrawl = 10, //Only crawl 10 pages
                MinCrawlDelayPerDomainMilliSeconds = 100, //Wait this many millisecs between requests
            };
            var crawler = new PoliteWebCrawler(config);
            crawler.ShouldCrawlPageDecisionMaker = (page, ctx) => {
                return new CrawlDecision { Allow = true };
            };

            crawler.PageCrawlCompleted += PageCrawlCompleted;//Several events available...

            var crawlResult = await crawler.CrawlAsync(new Uri("http://filehippo.com"));
        }

        private static async Task DemoSinglePageRequest()
        {
            var pageRequester = new PageRequester(new CrawlConfiguration(), new WebContentExtractor());

            var crawledPage = await pageRequester.MakeRequestAsync(new Uri("http://google.com"));
            Log.Logger.Information("{result}", new
            {
                url = crawledPage.Uri,
                status = Convert.ToInt32(crawledPage.HttpResponseMessage.StatusCode)
            });
        }

        private static void PageCrawlCompleted(object sender, PageCrawlCompletedArgs e)
        {
            var httpStatus = e.CrawledPage.HttpResponseMessage.StatusCode;
            var rawPageText = e.CrawledPage.Content.Text;
        }
    }
}

