using Abot2.Core;
using Abot2.Crawler;
using Abot2.Poco;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace tz2
{
    class Program
    {
        static void Main(string[] args) { MainAsync(args).Wait(); }
        static async Task MainAsync(string[] args)
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
                UserAgentString = "2019RLCrawlAThon",
                MaxPagesToCrawl = 0,
            };
            var start = new Uri("https://filehippo.com/download_ccleaner/post_download/");
            var crawler = new PoliteWebCrawler(config);

            crawler.ShouldCrawlPageDecisionMaker = (page, ctx) =>
            {
                if (page.Uri.Host != start.Host)
                {
                    return new CrawlDecision { Allow = false, Reason = "Different domain" };
                }
                return new CrawlDecision { Allow = true };
            };
            var files = new HashSet<string>();
            var decMaker = new CrawlDecisionMaker();
            crawler.ShouldDownloadPageContentDecisionMaker = (page, ctx) =>
            {
                if (page.Uri.AbsolutePath.EndsWith(".exe"))
                {
                    lock (files)
                    {
                        Console.WriteLine("Found file: " + page.Uri.AbsolutePath);
                        files.Add(page.Uri.AbsolutePath);
                    }
                }
                return decMaker.ShouldDownloadPageContent(page, ctx);
            };
            crawler.PageCrawlCompleted += Crawler_PageCrawlCompleted;
            var crawlResult = await crawler.CrawlAsync(start);
        }

        private static void Crawler_PageCrawlCompleted(object sender, PageCrawlCompletedArgs e)
        {
            string matchString = @"""(https:[^""]*)";

            MatchCollection matches = Regex.Matches(e.CrawledPage.Content.Text, matchString, RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            foreach (var match in matches.OfType<Match>())
            {
                try
                {
                    var uri = new Uri(match.Groups[1].Value);
                    if (match.Groups[1].Value.Contains(".exe")) { Debugger.Break(); }
                    e.CrawlContext.Scheduler.Add(new PageToCrawl(uri));
                }
                catch { }
            }
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
    }
}

