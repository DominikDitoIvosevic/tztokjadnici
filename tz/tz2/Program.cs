using Abot2.Core;
using Abot2.Crawler;
using Abot2.Poco;
using DataStructures;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
                MaxConcurrentThreads = 1,
            };
            var start = new Uri("https://filehippo.com/download_ccleaner/post_download/");
            var crawler = new PoliteWebCrawler(
                config,
                null,
                null,
                new Scheduler( false, null, new PriorityUriRepository()),
                null,
                null,
                null,
                null,
                null);

            crawler.ShouldCrawlPageDecisionMaker = (page, ctx) =>
            {
                var uri = page.Uri;
                return ShouldCrawl(start, uri);
            };
            crawler.ShouldCrawlPageLinksDecisionMaker = (page, ctx) => ShouldCrawl(start, page.Uri);

            var files = new HashSet<string>();
            var decMaker = new CrawlDecisionMaker();
            crawler.PageCrawlCompleted += Crawler_PageCrawlCompleted;
            crawler.PageCrawlCompleted += (sender, e) =>
            {
                if (e.CrawledPage.Uri.AbsolutePath.Contains(".exe"))
                {
                    lock (files)
                    {
                        Console.WriteLine("Found file: " + e.CrawledPage.Uri.AbsolutePath);
                        files.Add(e.CrawledPage.Uri.ToString());
                    }
                }
            };
            var crawlResult = await crawler.CrawlAsync(start);
        }

        private static CrawlDecision ShouldCrawl(Uri start, Uri uri)
        {
            if (uri.Host != start.Host)
            {
                return new CrawlDecision { Allow = false, Reason = "Different domain" };
            }

            bool isCulture = IsCultureLink(uri);

            if (isCulture)
            {
                return new CrawlDecision { Allow = false, Reason = "" };
            }

            if (new[] { "img", "imag", "doubleclick", "png", "jpg", "style", "script" }.Any(pp => uri.AbsolutePath.Contains(pp)))
            {
                return new CrawlDecision { Allow = false, Reason = "Ads or images" };
            }

            return new CrawlDecision { Allow = true };
        }

        private static bool IsCultureLink(Uri uri)
        {
            List<string> cc = Cultures;
            var isCulture = cc.Any(s => uri.AbsolutePath.Contains("/" + s + "/"));
            return isCulture;
        }

        private static List<string> Cultures = GetCultures();

        private static List<string> GetCultures()
        {
            var c = CultureInfo.GetCultures(CultureTypes.AllCultures);

            var c123 = c.Where(ss => ss.Name.Contains("-")).Select(sss => sss.Name.Substring(sss.Name.IndexOf("-") + 1)).Where(sss => sss.Length == 2 && !sss.Contains("-"));

            var cc = c.Select(c1 => c1.TwoLetterISOLanguageName)
                .Concat(c123).Select(cccc => cccc.ToLower())
                .Distinct().Where(ccc => ccc.Length == 2 && ccc != "en").ToList();
            cc.Sort();
            return cc;
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

    class PriorityUriRepository : IPagesToCrawlRepository
    {
        private ConcurrentPriorityQueue<(int, int, PageToCrawl)> q;
        public PriorityUriRepository()
        {
            q = new ConcurrentPriorityQueue<(int, int, PageToCrawl)>(Comparer<(int, int, PageToCrawl)>.Create((t1, t2) =>
            {
                if (t1.Item1 == t2.Item1) return t1.Item2 - t2.Item2;
                return t1.Item1 - t2.Item1;
            }));
        }

        public void Add(PageToCrawl page)
        {
            if (page.Uri.AbsolutePath.Contains("exe"))
            {
                q.Add((100, q.Count, page));
            }
            else
            {
                q.Add((0, q.Count, page));
            }
        }

        public void Clear()
        {
            q.Clear();
        }

        public int Count()
        {
            return q.Count;
        }

        public void Dispose()
        {
            q = null;
        }

        public PageToCrawl GetNext()
        {
            (int, int, PageToCrawl) res;
            q.TryTake(out res);
            return res.Item3;
        }
    }
}

