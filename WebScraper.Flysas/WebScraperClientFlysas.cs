#define FROMFILE
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

            clientHandler = new HttpClientHandler()
            {
                // AllowAutoRedirect = true,
                UseCookies = true,
                CookieContainer = cookieContainer,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            client = new HttpClient(clientHandler);
            client.AddDefaultRequestHeaders();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-insecure-requests", "1");
        }

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
            var formData = FillHomePageForm(homePage, query);

            // post back request to homePage
            var postBackRequest = new HttpRequestMessage(HttpMethod.Post, homepageUri)
            {
                Content = EncodeFormToMultiPartContent(formData)
            };

            client.DefaultRequestHeaders.Referrer = homepageUri;
            clientHandler.CookieContainer.Add(homepageUri, GetMockWtfpcCookie());
            clientHandler.CookieContainer.Add(homepageUri, GetMockSasLastSearchCookie(query));

#if !FROMFILE
            Thread.Sleep(2000);     // should I be nice and wait between requests? 
            HtmlDocument postBackPage = await client.GetHtmlDocumentAsync(postBackRequest, "data/_temp_2_postback_page.xhtml");       //temp bool value to save file
#else
            HtmlDocument postBackPage = new HtmlDocument();
            postBackPage.Load(@"data/_temp_2_postback_page.xhtml");
#endif
            // post back response data handling                                 // TODO: start count: after loading a page start count till next postback
            var postBackPageFormData = FillPostBackPageForm(postBackPage);

            var postBackScript = postBackPage.DocumentNode.SelectNodes("//script").Last().InnerText;
            var match = Regex.Match(postBackScript, @"'(https://.+)',.+(\d{4})\);", RegexOptions.IgnoreCase); // TODO: adjust regex to be match ms of any number of digits

            Uri redirectUri = match.Success ? new Uri(match.Groups[1].Value) : homepageUri;
            int waitingTime = match.Success ? int.Parse(match.Groups[2].Value) : 3000;

            // second postback to redirected page
            var tablePageRequest = new HttpRequestMessage(HttpMethod.Post, redirectUri)
            {
                Content = new FormUrlEncodedContent(postBackPageFormData)
            };

#if !FROMFILE 
            Thread.Sleep(waitingTime);             //before this remove the time passed from the load 
            HtmlDocument flightTablePage = await client.GetHtmlDocumentAsync(tablePageRequest, "data/_temp_3_tablePage_page.xhtml");
#else
            HtmlDocument flightTablePage = new HtmlDocument();
            flightTablePage.Load(@"data/_temp_3_tablePage_page.xhtml");
#endif
            // I don't think I get the table anymore .. is it my IP being blocked or is the page changed? 

            //select only direct or redirected in Oslo, with the chepeast Fare; 
            var allOutbound = ParseFlightsData(flightTablePage, "outbound");

            var outbounds = allOutbound.Where(data => data.Connection.Equals("") || data.Connection.Equals("Oslo"))
                                       .Select(data => new FlightDataModel
                                       {
                                           Departure = data.Departure,
                                           Arrival = data.Arrival,
                                           Connection = data.Connection,
                                           DepTime = query.DepDate + data.DepTime,
                                           ArrTime = query.DepDate + data.ArrTime,
                                           Price = data.Fares.MinByPrice().Price,
                                           Taxes = data.Fares.MinByPrice().Taxes
                                       }).ToList();

            //select only direct or redirected in Oslo, with the chepeast Fare;     //leaving as duplicates in case instructions need to change 
            var allInbound = ParseFlightsData(flightTablePage, "inbound");

            var inbounds = allInbound.Where(data => data.Connection.Equals("") || data.Connection.Equals("Oslo"))
                                     .Select(data => new FlightDataModel
                                     {
                                         Departure = data.Departure,
                                         Arrival = data.Arrival,
                                         Connection = data.Connection,
                                         DepTime = query.RetDate + data.DepTime,
                                         ArrTime = query.RetDate + data.ArrTime,
                                         Price = data.Fares.MinByPrice().Price,
                                         Taxes = data.Fares.MinByPrice().Taxes
                                     }).ToList();

            //combine inbound/outbound  | Cartesian Product | for each outbound select each inbound
            var collectedData = outbounds.SelectMany(outbound =>
                                                     inbounds.Select(inbound => new RoundTripFlightData
                                                     {
                                                         Outbound = outbound,
                                                         Inbound = inbound
                                                     }));

            disk.SaveCollectedData(collectedData);
        }

        private IEnumerable<SasFlightData> ParseFlightsData(HtmlDocument page, string direction)
        {
            var flightsData = new List<SasFlightData>();

            var flightRows = page.DocumentNode.SelectNodes($"//div[contains(@class, '{direction}')]//tr[contains (@id, 'idLine')]");
            foreach (var row in flightRows)
            {
                SasFlightData flight = ParseFlightRow(row);
                flightsData.Add(flight);
            }
            return flightsData;
        }

        private SasFlightData ParseFlightRow(HtmlNode row)
        {
            var result = new SasFlightData();
            try
            {
                // Get Airports
                var airports = row.SelectSingleNode("following-sibling::tr[1]").SelectNodes(".//td/span[@class='route']/span[@class='location']");

                result.Departure = airports.First().SelectSingleNode(".//*[@class='airport']").InnerText;
                result.Arrival = airports.Last().SelectSingleNode(".//*[@class='airport']").InnerText;

                result.Connection = airports[1].InnerText.Contains(result.Arrival) ? "" : airports[1].InnerText; //if  it not a connected flight then this will contain arrival

                // Get Time -- I am ignoring the +1 days for now
                var time = row.SelectNodes(".//td[@class='time']/*[@class='time']");
                result.DepTime = ParseTime(time[0].InnerText);
                result.ArrTime = ParseTime(time[1].InnerText);

                // Get fare Info
                result.Fares = GetAvailableFares(row);

            }
            catch (Exception e)
            {
                System.Console.WriteLine("Error Parsing flight Data: {0}", e.Message);
            }
            return result;
        }


        private List<FareInfo> GetAvailableFares(HtmlNode flightRow)
        {
            var fares = new List<FareInfo>();
            var fareNodes = flightRow.SelectNodes("td[contains(@class, 'fare')]");

            foreach (var node in fareNodes)
            {
                FareInfo fare = GetFareData(node);
                if (fare != null) fares.Add(fare);
            }
            return fares;
        }

        private FareInfo GetFareData(HtmlNode node)
        {
            FareInfo fare = new FareInfo();

            // find a fare Id match
            Match fareIdMatch = Regex.Match(node.Id, @"^reco_(\d.+)$");
            if (fareIdMatch.Success)
                fare.Id = fareIdMatch.Groups[1].Value;
            else return null;   // no id

            // get all script texts
            string scripts = node.SelectNodes("//script")
                                 .Select(s => s.InnerText)
                                 .Aggregate((a, b) => a + b);

            // find fare price
            Match priceMatch = Regex.Match(scripts, $@"price_{fare.Id}.+'data-price','(\d+.\d+)'");
            if (priceMatch.Success)
            {
                string price = priceMatch.Groups[1].Value;

                Match taxMatch = Regex.Match(scripts, $@"'price':'{price}'[^']+'tax':'(\d+[.]\d+)'");
                if (taxMatch.Success)
                    fare.Taxes = decimal.Parse(taxMatch.Groups[1].Value);
                else return null;

                fare.Price = decimal.Parse(price);
                return fare;
            }
            else return null;  //no price
        }

        private Dictionary<string, string> FillPostBackPageForm(HtmlDocument page)
        {
            //get successful controls
            var form = page.DocumentNode.SelectNodes("//input")
                                        .Where(input => !Regex.IsMatch(input.GetAttributeValue("name", ""), @"^btnSubmit.+$"))
                                        .ToDictionary(name => name.GetAttributeValue("name", ""), value => value.GetAttributeValue("value", ""));

            Regex lessThanRgx = new Regex("&lt;");
            form["SO_GL"] = lessThanRgx.Replace(form["SO_GL"], "<");
            form["__EVENTTARGET"] = "btnSubmitAmadeus";

            return form;
        }

        private Dictionary<string, string> FillHomePageForm(HtmlDocument page, QueryOptions query)
        {
            var tripType = query.IsRoundTrip ? "roundtrip" : "oneway";
            var formControlNodes = page.DocumentNode.SelectNodes("//input[@name][not(@type='submit')]|//select[@name]")
                                                    .Where(node => !Regex.IsMatch(node.Attributes["name"].Value, @"MainFormBorderPanel\$url$", RegexOptions.IgnoreCase))
                                                    .Where(node =>
                                                    {
                                                        if ((node.Attributes.Contains("type") && node.Attributes["type"].Value.Equals("radio")))
                                                        {
                                                            bool rightTripType = node.GetAttributeValue("value", "").Equals(tripType);    // to not hardcode this, I should leave all radios in the form data and
                                                            bool isShowDates = node.GetAttributeValue("value", "").Equals("Show selected dates");  // and remove unnecessary after filling the form from query
                                                            return (rightTripType || isShowDates);
                                                        }
                                                        else return true;
                                                    });

            var mockFormControls = GetMockHomePageFormData(query);
            var filledFormData = formControlNodes.ToDictionary(form => form.GetAttributeValue("name", ""),      // Key
                                                               form =>
                                                               {
                                                                   var matchingControl = mockFormControls.FirstOrDefault(mockControl => Regex.IsMatch(form.GetAttributeValue("name", ""), mockControl.Key));
                                                                   return matchingControl.Value ?? form.GetAttributeValue("value", "");
                                                               });
            return filledFormData;
        }

        private MultipartFormDataContent EncodeFormToMultiPartContent(Dictionary<string, string> form)
        {
            var formDataContent = new MultipartFormDataContent("----WebKitFormBoundaryorq3ASaOYcTSG1JW");
            foreach (var controlName in form.Keys.ToList())
            {
                var contentPart = new StringContent(form[controlName]);
                contentPart.Headers.Clear(); //remove unnecessary headers

                formDataContent.Add(contentPart, $"\"{controlName}\"");
            }
            return formDataContent;
        }

        //this only has the data that is actually changing from the page load to after selecting the right dates
        private Dictionary<string, string> GetMockHomePageFormData(QueryOptions query)
        {
            var currDate = $"{DateTime.Now:ddd MMM d yyyy HH:mm:ss} GMT+0300(FLE Daylight Time)";
            var endDate = $"{DateTime.Now.AddYears(1):ddd MMM d yyyy HH:mm:ss} GMT+0300(FLE Daylight Time)";

            var data = new Dictionary<string, string> {
                    {"__EVENTTARGET",        "ctl00$FullRegion$MainRegion$ContentRegion$ContentFullRegion$ContentLeftRegion$CEPGroup1$CEPActive$cepNDPRevBookingArea$Searchbtn$ButtonLink"},
                    {"hiddenIntercont$",     "False"},
                    {"hiddenDomestic$",      "SE,GB"},
                    {"hiddenFareType$",      "A"},
                    {"txtFrom$",             "Stockholm, Sweden - Arlanda (ARN)"},
                    {"hiddenFrom$",          query.Departure},
                    {"txtTo$",               "London, United Kingdom - Heathrow (LHR)"},
                    {"hiddenTo$",            query.Arrival},
                    {"hiddenOutbound$",      query.DepDate.ToString("yyyy-MM-dd")},
                    {"hiddenReturn$",        query.RetDate.ToString("yyyy-MM-dd")},
                    {"hiddenStoreCalDates$", $"{currDate},{currDate},{endDate}"},
                    {"selectOutbound$",      $"{DateTime.Now.Year}-{DateTime.Now.Month}-01"},
                    {"selectReturn$",        $"{DateTime.Now.Year}-{DateTime.Now.Month}-01"},
                    {"TypeAdult$",           "1"},
                    {"TypeChild211$",        "0"},
                    {"TypeInfant$",          "0"},
                    {"ddlFareTypeSelector$", "A"}
            };
            return data;
        }

        // SASLastSearch cookie mimicking
        private Cookie GetMockSasLastSearchCookie(QueryOptions query)
        {
            var value = $"{{\"origin\":\"{query.Departure}\",\"destination\":\"{query.Arrival}\"," +
                        $"\"outward\":\"{query.DepDate:yyyyMMdd}\",\"inward\":\"{query.RetDate:yyyyMMdd}\"," +
                        $"\"adults\":\"1\",\"children\":\"0\",\"infants\":\"0\",\"youths\":\"NaN\",\"lpc\":\"false\"," +
                        $"\"oneway\":\"{(!query.IsRoundTrip).ToString().ToLower()}\",\"rtf\":\"false\",\"rcity\":\"false\"}}";
                                    
            var valueEncoded = Uri.EscapeUriString(value);
            var cookie = new Cookie("SASLastSearch", $"\"{valueEncoded}\"", "/", domain)
            {
                Expires = query.DepDate
            };
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
            var cookie = new Cookie("WT_FPC", $"\"{value}\"", "/", domain)
            {
                Expires = curDate.AddYears(10)
            };
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