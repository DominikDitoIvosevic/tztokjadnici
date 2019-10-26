using Abot2.Core;
using Abot2.Crawler;
using Abot2.Poco;
using Abot2.Util;
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
                .MinimumLevel.Warning()
                .WriteTo.Console()
                .CreateLogger();

            Log.Logger.Information("Demo starting up!");

            await DemoSimpleCrawler();
        }

        private static async Task DemoSimpleCrawler()
        {
            var config = new CrawlConfiguration
            {
                UserAgentString = "2019RLCrawlAThon",
                MaxPagesToCrawl = 0,
                MinCrawlDelayPerDomainMilliSeconds = 10,
            };
            var start = new Uri("https://filehippo.com/");
            var crawler = new PoliteWebCrawler(
                config,
                new BetterDecisionMaker(start),
                null,
                new Scheduler(false, null, new PriorityUriRepository()),
                null,
                null,
                null,
                null,
                null);

            var files = new HashSet<string>();
            var decMaker = new CrawlDecisionMaker();
            crawler.PageCrawlCompleted += Crawler_PageCrawlCompleted;
            crawler.PageCrawlCompleted += (sender, e) =>
            {
                if (e.CrawledPage.Uri.AbsolutePath.Contains(".exe"))
                {
                    lock (files)
                    {
                        Console.WriteLine("Found file: " + e.CrawledPage.Uri.Host + e.CrawledPage.Uri.LocalPath);
                        Console.WriteLine(e.CrawledPage.CrawlDepth);
                        files.Add(e.CrawledPage.Uri.ToString());
                    }
                }
            };
            var crawlResult = await crawler.CrawlAsync(start);
        }

        private static void Crawler_PageCrawlCompleted(object sender, PageCrawlCompletedArgs e)
        {

            if (e.CrawledPage.Uri.ToString() == "https://filehippo.com/windows/browsers/")
            {
                //Debugger.Break();
            }
            string matchString = @"""(https:[^""]*)";

            //Console.WriteLine("start rgx");
            //var sw = Stopwatch.StartNew();
            MatchCollection matches = Regex.Matches(e.CrawledPage.Content.Text, matchString, RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            //if (sw.ElapsedMilliseconds > 100) Console.WriteLine(sw.ElapsedMilliseconds);
            //Console.WriteLine("end rgx");

            foreach (var match in matches.OfType<Match>())
            {
                try
                {
                    var uri = new Uri(match.Groups[1].Value);

                    if (BetterDecisionMaker.ShouldCrawl(uri, null).Allow)
                    {
                        e.CrawlContext.Scheduler.Add(new PageToCrawl(uri));
                    }
                }
                catch { }
            }
        }
    }

    class PriorityUriRepository : IPagesToCrawlRepository
    {
        private PriorityQueue<(int, PageToCrawl)> q;
        public PriorityUriRepository()
        {
            q = new ConcurrentPriorityQueue<(int, PageToCrawl)>(Comparer<(int, PageToCrawl)>.Create((t1, t2) =>
            {
                return t1.Item1 - t2.Item1;
            }));
        }

        public void Add(PageToCrawl page)
        {
            lock (q)
            {
                var score = 0;
                if (page.Uri.ToString().Contains(".exe")) score += 100;
                //if (page.Uri.ToString().Contains("post_download")) score += 100;
                if (page.Uri.ToString().Contains("download")) score += 10;
                if (page.Uri.ToString().Contains("dl")) score += 1;
                score -= page.CrawlDepth;
                q.Add((score, page));
            }
        }

        public void Clear()
        {
            lock (q)
            {
                q.Clear();
            }
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
            lock (q)
            {
                if (q.Count > 0) return q.Take().Item2;
                return null;
            }
        }
    }

    class BetterDecisionMaker : ICrawlDecisionMaker
    {
        private readonly CrawlDecisionMaker def;
        private readonly Uri start;
        public BetterDecisionMaker(Uri start)
        {
            def = new CrawlDecisionMaker();
            this.start = start;
        }

        private static string GetDomain(Uri uri) => string.Join(".", uri.Host.Split('.').Reverse().Take(2).Reverse());

        public static CrawlDecision ShouldCrawl(Uri page, Uri start)
        {
            if (start != null && GetDomain(page) != GetDomain(start))
            {
                return new CrawlDecision { Allow = false, Reason = "Different domain" };
            }

            bool isCulture = IsCultureLink(page);

            if (isCulture)
            {
                return new CrawlDecision { Allow = false, Reason = "" };
            }

            if (new[] { "img", "imag", "doubleclick", "png", "jpg", "style", "script", "news" }.Any(pp => page.AbsolutePath.Contains(pp)))
            {
                return new CrawlDecision { Allow = false, Reason = "Ads or images" };
            }
            return new CrawlDecision { Allow = true };
        }

        public CrawlDecision ShouldCrawlPage(PageToCrawl page, CrawlContext crawlContext)
        {
            //return new CrawlDecision { Allow = false, Reason = "fuck off" };

            var dec = ShouldCrawl(page.Uri, start);
            if (!dec.Allow) return dec;
            return def.ShouldCrawlPage(page, crawlContext);
        }

        public CrawlDecision ShouldCrawlPageLinks(CrawledPage page, CrawlContext crawlContext)
        {
            //return new CrawlDecision { Allow = false, Reason = "fuck off" };

            var dec = ShouldCrawl(page.Uri, start);
            if (!dec.Allow) return dec;
            return def.ShouldCrawlPageLinks(page, crawlContext);
        }

        public CrawlDecision ShouldDownloadPageContent(CrawledPage crawledPage, CrawlContext crawlContext)
        {
            //return new CrawlDecision { Allow = false, Reason = "fuck off" };

            return def.ShouldDownloadPageContent(crawledPage, crawlContext);
        }

        public CrawlDecision ShouldRecrawlPage(CrawledPage crawledPage, CrawlContext crawlContext)
        {
            return new CrawlDecision { Allow = false, Reason = "fuck off" };

            return def.ShouldRecrawlPage(crawledPage, crawlContext);
        }

        private static bool IsCultureLink(Uri page)
        {
            List<string> cc = Cultures;
            var isCulture = cc.Any(s => page.AbsolutePath.Contains("/" + s + "/"));
            return isCulture;
        }

        public static List<string> Cultures = GetCultures();

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

    }
}

