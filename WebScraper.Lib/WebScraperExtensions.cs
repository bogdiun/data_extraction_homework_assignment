using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WebScraper.Lib {

    public static class WebScraperExtensions {

        public static void AddDefaultRequestHeaders(this HttpClient client) {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 6.2; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Charset", "ISO-8859-1");
        }

        public static async Task<string> GetCrawlingRulesAsync(this HttpClient client, string hostUri) =>
            await client.GetStringAsync($"{hostUri}robots.txt");

        public static async Task<HtmlDocument> GetHtmlDocumentAsync(this HttpClient client, HttpRequestMessage request) =>
            await client.GetHtmlDocumentAsync(request, String.Empty);

        public static async Task<HtmlDocument> GetHtmlDocumentAsync(this HttpClient client, HttpRequestMessage request, string filePath) {
            HttpResponseMessage response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var htmlDocument = new HtmlDocument();
            using (var responseStream = await response.Content.ReadAsStreamAsync())
                htmlDocument.Load(responseStream);

            //optionally save xhtml file in project's root directory
            if (filePath != String.Empty) {
                Directory.CreateDirectory(@"data/");

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                using (var stream = new StreamWriter(fileStream)) {
                    htmlDocument.Save(stream);
                }
            }
            return htmlDocument;
        }

        public static FareInfo MinByPrice(this IEnumerable<FareInfo> source) {
            var e = source.GetEnumerator();
            if (source == null) throw new ArgumentNullException("source can't be null");
            if (!e.MoveNext()) throw new InvalidOperationException("source can't be empty");

            FareInfo min = e.Current;

            while (e.MoveNext()) {
                min = min.Price > e.Current.Price ? e.Current : min;
            }
            return min;
        }
    }
}
