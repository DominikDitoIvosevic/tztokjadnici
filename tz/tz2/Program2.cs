using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace tz2
{
    class Program2
    {
        static void Main(string[] args)
        {
            Asyn();
            Console.Read();
        }

        static async void Asyn()
        {
            HttpClient x = new HttpClient();
            HttpResponseMessage g = await x.GetAsync("https://filehippo.com");

            string s = await g.Content.ReadAsStringAsync();

            string matchString = @"""https:[^""]*";
            
            MatchCollection matches = Regex.Matches(s, matchString, RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            List<Match> list = ToList<Match>(matches);
            var ll = list.Select(m => m.Value).ToList();

            Console.WriteLine(g.Content);
        }

        private static List<T> ToList<T>(System.Collections.IEnumerable matches)
        {
            var list = new List<T>();
            foreach (T match in matches)
            {
                list.Add(match);
            }

            return list;
        }
    }
}
