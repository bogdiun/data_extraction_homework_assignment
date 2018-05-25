// #define FROMFILE
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WebScraper.Lib;
using HtmlAgilityPack;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Text;
using System.Collections.Generic;

namespace WebScraper.Flysas
{

    public class WebScraperClientFlysas
    {
        private static readonly Uri homepageUri = new Uri("https://www.flysas.com/en/");
        private static readonly string domain = "www.flysas.com";
        private HttpClientHandler clientHandler;
        private HttpClient client;
        private IStorage disk = new RoundTripDataFileStorage();

        public WebScraperClientFlysas()
        {
            var cookieContainer = new CookieContainer();
            this.clientHandler = new HttpClientHandler()
            {
                UseCookies = true,
                AllowAutoRedirect = true,
                CookieContainer = cookieContainer,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            this.client = new HttpClient(clientHandler);
            this.client.AddDefaultRequestHeaders();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-insecure-requests", "1");
        }

        // TODO refactor, naming and methods being too specif, not reusable .. however this is sort of page specific so not sure.

        public async Task StartScraperAsync(QueryOptions query)
        {

#if !FROMFILE // -- INITIAL GET REQUEST

            HttpRequestMessage homePageRequest = new HttpRequestMessage(HttpMethod.Get, homepageUri);
            HtmlDocument homePage = await client.GetHtmlDocumentAsync(homePageRequest, "data/_temp_1_home_page.xhtml");
            // System.Console.WriteLine(clientHandler.CookieContainer.GetCookieHeader(homepageUri));
#else
            HtmlDocument homePage = new HtmlDocument();
            homePage.Load(@"data/_temp_1_home_page.xhtml");
#endif
            // response data handling
            var formDataBody = GetHomePageData(homePage);
            MultipartFormDataContent postBackRequestContent = GetHomePageFormDataContent(formDataBody, query);

            client.DefaultRequestHeaders.Referrer = homepageUri;

            clientHandler.CookieContainer.Add(homepageUri, GetMockWtfpcCookie());
            clientHandler.CookieContainer.Add(homepageUri, GetMockSasLastSearchCookie(query));

#if !FROMFILE
            // post back request to homePage
            Thread.Sleep(2000);     // should I be nice and wait between requests? 
            var postBackRequest = new HttpRequestMessage(HttpMethod.Post, homepageUri);
            postBackRequest.Content = postBackRequestContent;

            HtmlDocument postBackPage = await client.GetHtmlDocumentAsync(postBackRequest, "data/_temp_2_postback_page.xhtml");       //temp bool value to save file
#else
            HtmlDocument postBackPage = new HtmlDocument();
            postBackPage.Load(@"data/_temp_2_postback_page.xhtml");
#endif
            // post back response data handling                                 // TODO: start count: after loading a page start count till next postback
            var postBackPageData = GetPostBackPageData(postBackPage);
            FormUrlEncodedContent tableRequestContent = new FormUrlEncodedContent(postBackPageData);

            var postBackScript = postBackPage.DocumentNode.SelectNodes("//script").Last().InnerText;
            var match = Regex.Match(postBackScript, @"'(https://.+)',.+(\d{4})\);", RegexOptions.IgnoreCase); // TODO: adjust regex to be match ms of any number of digits

            Uri redirectUri = match.Success ? new Uri(match.Groups[1].Value) : homepageUri;
            int waitingTime = match.Success ? int.Parse(match.Groups[2].Value) : 3000;

#if !FROMFILE 
            // second postback to redirected page
            Thread.Sleep(waitingTime);             //before this remove the time passed from the load 
            var tablePageRequest = new HttpRequestMessage(HttpMethod.Post, redirectUri);
            tablePageRequest.Content = tableRequestContent;

            HtmlDocument page = await client.GetHtmlDocumentAsync(tablePageRequest, "data/_temp_3_tablePage_page.xhtml");
#else
            HtmlDocument page = new HtmlDocument();
            page.Load(@"data/_temp_3_tablePage_page.xhtml");
#endif


            //select only direct or redirected in Oslo, with the chepeast Fare; 
            var outboundFlightsData = ParseFlightsDataFromPage(page, "outbound");
            var outbounds = outboundFlightsData.Where(o => o.Connection.Equals("") || o.Connection.Equals("Oslo"))
                                                   .Select(f => new FlightDataModel
                                                   {
                                                       Departure = f.Departure,
                                                       Arrival = f.Arrival,
                                                       Connection = f.Connection,
                                                       DepTime = query.DepDate + f.DepTime,
                                                       ArrTime = query.DepDate + f.ArrTime,
                                                       Price = f.Fares.MinByPrice().Price,
                                                       //    Taxes = f.Fares.MinByPrice().Taxes
                                                   }).ToList();

            //select only direct or redirected in Oslo, with the chepeast Fare;     //leaving as duplicates in case instructions need to change 
            var inboundFlightsData = ParseFlightsDataFromPage(page, "inbound");
            var inbounds = outboundFlightsData.Where(o => o.Connection.Equals("") || o.Connection.Equals("Oslo"))
                                                   .Select(f => new FlightDataModel
                                                   {
                                                       Departure = f.Departure,
                                                       Arrival = f.Arrival,
                                                       Connection = f.Connection,
                                                       DepTime = query.RetDate + f.DepTime,
                                                       ArrTime = query.RetDate + f.ArrTime,
                                                       Price = f.Fares.MinByPrice().Price,
                                                       Taxes = f.Fares.MinByPrice().Taxes
                                                   }).ToList();

            //combine inbound/outbound
            var collectedData = outbounds.SelectMany(outbound =>
                                                     inbounds.Select(inbound => new RoundTripFlightData
                                                     {
                                                         Outbound = outbound,
                                                         Inbound = inbound
                                                     }));

            disk.SaveCollectedData(collectedData);
        }

        private IEnumerable<SasFlightData> ParseFlightsDataFromPage(HtmlDocument page, string table)
        {
            var flightsData = new List<SasFlightData>();

            var flightNodes = page.DocumentNode.SelectNodes($"//div[contains(@class, '{table}')]//tr[contains (@id, 'idLine')]");
            foreach (var node in flightNodes)
            {
                SasFlightData flight = ParseFlightNode(node);
                flightsData.Add(flight);
            }
            return flightsData;
        }

        private SasFlightData ParseFlightNode(HtmlNode node)
        {
            var result = new SasFlightData();
            try
            {
                // Get Airports
                var airports = node.SelectSingleNode("following-sibling::tr[1]").SelectNodes(".//td/span[@class='route']/span[@class='location']");

                result.Departure = airports.First().SelectSingleNode(".//*[@class='airport']").InnerText;
                result.Arrival = airports.Last().SelectSingleNode(".//*[@class='airport']").InnerText;

                result.Connection = airports[1].InnerText.Contains(result.Arrival) ? "" : airports[1].InnerText;

                // Get Time -- I am ignoring the +1 days for now
                var time = node.SelectNodes(".//td[@class='time']/*[@class='time']");
                result.DepTime = ParseTime(time[0].InnerText);
                result.ArrTime = ParseTime(time[1].InnerText);

                // Get fare Info
                result.Fares = GetAvailableFares(node);

            }
            catch (Exception e)
            {
                System.Console.WriteLine("Error Parsing flight Data: {0}", e.Message);
            }
            return result;
        }


        private List<FareInfo> GetAvailableFares(HtmlNode node)
        {
            var fares = new List<FareInfo>();
            var fareNodes = node.SelectNodes("td[contains(@class, 'fare')]");

            foreach (var n in fareNodes)
            {
                FareInfo fare = new FareInfo();

                Match matcher = Regex.Match(n.Id, @"^reco_(\d.+)$");
                fare.Id = matcher.Groups[1].Value;

                bool success = TryUpdateFarePrice(fare, node);

                if (success) fares.Add(fare);
            }
            return fares;
        }

        private bool TryUpdateFarePrice(FareInfo fare, HtmlNode html)
        {
            string scripts = html.SelectNodes("//script")
                                 .Select(s => s.InnerText)
                                 .Aggregate((a, b) => a + b);

            //could do a trycatch
            Match matcher = Regex.Match(scripts, $@"price_{fare.Id}.+'data-price','(\d+.\d+)'");
            if (matcher.Success)
            {
                fare.Price = decimal.Parse(matcher.Groups[1].Value);

                Match recoMatch = Regex.Match(scripts, $@"recoHidden_{fare.Id}', '(\d+)'");
                if (recoMatch.Success)
                {
                    string reco = recoMatch.Groups[1].Value;

                    Match taxMatch = Regex.Match(scripts, $@"'tax':'(\d+.\d+)'.+recommendation, "".*""{reco}""", RegexOptions.Multiline);
                    fare.Taxes = taxMatch.Success ? decimal.Parse(taxMatch.Groups[2].Value) : -1;
                }
                return true;
            }
            else return false;
        }
        // can't figure out a more efficient way for now. 
        // And no time to attempt headless engine for simplicity sake, maybe later?
        // or try Jint with or without Knyaz.Optimus for js 

        private Dictionary<string, string> GetPostBackPageData(HtmlDocument page)
        {
            var data = page.DocumentNode.SelectNodes("//input")
                                        .Where(n => !Regex.IsMatch(n.GetAttributeValue("name", ""), @"^btnSubmit.+$"))
                                        .ToDictionary(k => k.GetAttributeValue("name", ""), v => v.GetAttributeValue("value", ""));

            Regex lessThanRgx = new Regex("&lt;");
            data["SO_GL"] = lessThanRgx.Replace(data["SO_GL"], "<");
            data["__EVENTTARGET"] = "btnSubmitAmadeus";

            // postBackPageData.ToList().ForEach(i => System.Console.WriteLine($"{i.Key}: {i.Value}"));
            return data;
        }

        private Dictionary<string, string> GetHomePageData(HtmlDocument page)
        {
            var dataTags = page.DocumentNode.SelectNodes("//input[@name][not(@type='submit')]|//select[@name]")
                                .Where(d => !Regex.IsMatch(d.Attributes["name"].Value, @"MainFormBorderPanel\$url$", RegexOptions.IgnoreCase))
                                .Where(n =>
                                {
                                    if ((n.Attributes.Contains("type") && n.Attributes["type"].Value.Equals("radio")))
                                    {
                                        bool isRoundtrip = n.GetAttributeValue("value", "").Equals("roundtrip");
                                        bool isShowDates = n.GetAttributeValue("value", "").Equals("Show selected dates");
                                        return (isRoundtrip || isShowDates);
                                    }
                                    else return true;
                                });

            Dictionary<string, string> dataPairs = dataTags.ToDictionary(k => k.GetAttributeValue("name", ""), v => v.GetAttributeValue("value", ""));
            return dataPairs;
        }

        //generates the formData for the first Post request
        private MultipartFormDataContent GetHomePageFormDataContent(Dictionary<string, string> data, QueryOptions query)
        {
            var formDataContent = new MultipartFormDataContent("----WebKitFormBoundaryorq3ASaOYcTSG1JW");

            var mockData = GetMockHomePageFormData(query);
            foreach (var name in data.Keys.ToList())
            {

                var pairMatch = mockData.FirstOrDefault(d => Regex.IsMatch(name, d.Key));
                data[name] = pairMatch.Value ?? data[name];

                //add as string content
                var content = new StringContent(data[name]);
                //remove unnecessary headers
                content.Headers.Clear();

                formDataContent.Add(content, $"\"{name}\"");
            }
            return formDataContent;
        }

        //for now mock data for the specific request
        private Dictionary<string, string> GetMockHomePageFormData(QueryOptions query)
        {
            var currDate = $"{DateTime.Now:ddd MMM d yyyy HH:mm:ss} GMT+0300(FLE Daylight Time)";
            var endDate = $"{DateTime.Now.AddYears(1):ddd MMM d yyyy HH:mm:ss} GMT+0300(FLE Daylight Time)";

            var data = new Dictionary<string, string> {
                    {"__EVENTTARGET",       "ctl00$FullRegion$MainRegion$ContentRegion$ContentFullRegion$ContentLeftRegion$CEPGroup1$CEPActive$cepNDPRevBookingArea$Searchbtn$ButtonLink"},
                    {"hiddenIntercont$",    "False"},
                    {"hiddenDomestic$",     "SE,GB"},
                    {"hiddenFareType$",     "A"},
                    {"txtFrom$",            "Stockholm, Sweden - Arlanda (ARN)"},
                    {"hiddenFrom$",         query.Departure},
                    {"txtTo$",              "London, United Kingdom - Heathrow (LHR)"},
                    {"hiddenTo$",           query.Arrival},
                    {"hiddenOutbound$",     "2018-06-04"},  //depdate
                    {"hiddenReturn$",       "2018-06-10"},    //retdate
                    {"hiddenStoreCalDates$", $"{currDate},{currDate},{endDate}"},
                    {"selectOutbound$",     $"{DateTime.Now.Year}-{DateTime.Now.Month}-01"},
                    {"selectReturn$",       $"{DateTime.Now.Year}-{DateTime.Now.Month}-01"},
                    {"TypeAdult$",          "1"},
                    {"TypeChild211$",       "0"},
                    {"TypeInfant$",         "0"},
                    {"ddlFareTypeSelector$", "A"}
            };
            return data;
        }

        // SASLastSearch cookie mimicking
        private Cookie GetMockSasLastSearchCookie(QueryOptions query)
        {
            var value = @"{""origin"":""ARN"",""destination"":""LHR"",""outward"":""20180604"",""inward"":""20180610"",""adults"":""1"",""children"":""0"",""infants"":""0"",""youths"":""NaN"",""lpc"":""false"",""oneway"":""false"",""rtf"":""false"",""rcity"":""false""}";

            var valueEncoded = Uri.EscapeUriString(value);
            var cookie = new Cookie("SASLastSearch", $"\"{valueEncoded}\"", "/", domain);
            cookie.Expires = query.DepDate;
            return cookie;
        }

        //Generates the cookie mimicking js script
        private Cookie GetMockWtfpcCookie()
        {
            var random = new Random();
            var id = new StringBuilder("2");

            var curDate = DateTime.Now;
            var curDateJs = DateTime.Now.Ticks - new DateTime(1970, 1, 1).Ticks;
            var curDateOffset = curDateJs - 2297136;

            for (int i = 2; i <= (32 - curDateJs.ToString().Length); i++)
            {
                var randInt = Convert.ToInt32(Math.Floor(random.NextDouble() * 16.0));
                var rand = Convert.ToString(randInt, 16);
                id.Append(rand);
            }

            id.Append(curDateOffset.ToString());
            var lv = curDateJs.ToString();
            var ss = curDateOffset.ToString();

            var value = $"id={id.ToString()}:lv={lv}:ss={ss}";
            var cookie = new Cookie("WT_FPC", $"\"{value}\"", "/", domain);
            cookie.Expires = curDate.AddYears(10);
            return cookie;
        }

        private TimeSpan ParseTime(string timeString)
        {
            Regex timeRegex = new Regex(@"([01][0-9]|[2][0-3])[:-]([0-5][0-9])");
            Match match = timeRegex.Match(timeString);
            if (match.Success)
            {
                int hours = int.Parse(match.Groups[1].Value);
                int minutes = int.Parse(match.Groups[2].Value);
                return new TimeSpan(hours, minutes, 0);
            }
            else return TimeSpan.Zero;
        }
    }
}