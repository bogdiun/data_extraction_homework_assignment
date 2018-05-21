using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using WebScraper.Lib;

namespace WebScraper.Norwegian {
    public class WebScraperClientNorwegian {
        private static readonly Uri hostname = new Uri("https://www.norwegian.com/");
        // private string crawlingRules;
        private HttpClientHandler handler;
        private HttpClient client;
        private Storage disk = new Storage();

        //initialize and setup client, get crawling rules
        public WebScraperClientNorwegian() {
            var cookieContainer = new CookieContainer();
            this.handler = new HttpClientHandler() {
                // CookieContainer = cookieContainer,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = false
            };
            client = new HttpClient(handler);
            client.AddDefaultRequestHeaders();
        }

        public Uri GetUri(QueryOptions opt) {
            var result = new StringBuilder(hostname.OriginalString);

            result.Append($"en/ipc/availability/avaday?D_City={opt.Departure}&A_City={opt.Arrival}&")
                  .Append($"D_Day={opt.DepDate.Day:00}&D_Month={opt.DepDate.Year:0000}{opt.DepDate.Month:00}&");

            if (opt.DepFareType != null && !opt.DepFareType.Equals("") && opt.DepFlight != null) {
                result.Append($"dFlight={opt.DepFlight}&dCabinFareType={opt.DepFareType}&");
            }

            if (opt.IsReturnTrip) {
                result.Append($"R_Day={opt.RetDate.Day:00}&R_Month={opt.RetDate.Year:0000}{opt.RetDate.Month:00}&");
                if (opt.RetFareType != null && opt.RetFlight != null) {
                    result.Append($"rFlight={opt.RetFlight}&rCabinFareType={opt.RetFareType}&");
                }
            } else {
                result.Append("TripType=1&");
            }

            result.Append($"IncludeTransit={!opt.IsDirect}&CurrencyCode=EUR");

            return new Uri(result.ToString());
        }

        public async Task StartScraperAsync(QueryOptions query) {
            try {
                var collectedData = new List<FlightDataModel>();
                // client.GetCrawlingRulesAsync(hostname).Wait();
                var getRequest = new HttpRequestMessage(HttpMethod.Get, GetUri(query));
                var htmlDocument = await client.GetHtmlDocumentAsync(getRequest);

                var departingFlights = ParseFlightTable(htmlDocument, "//div[@id='avaday-outbound-result']//table[@class='avadaytable']/tbody");

                // change query for each flight to update taxes, look if it is possible to not do that..
                Regex flightRegex = new Regex(@"\d\|([A-Za-z0-9]+)\|(\d)\|");
                foreach (var fl in departingFlights) {
                    Match match = flightRegex.Match(fl.CheapestFare.Type);
                    if (match.Success) {
                        query.DepFlight = match.Groups[1].Value;
                        query.DepFareType = match.Groups[2].Value;

                        getRequest = new HttpRequestMessage(HttpMethod.Get, GetUri(query));
                        var nextDocument = await client.GetHtmlDocumentAsync(getRequest);

                        DateTime dep = query.DepDate + ParseTime(fl.DepTime);
                        DateTime arr = query.DepDate + ParseTime(fl.ArrTime);
                        decimal tax = GetTaxData(nextDocument);

                        collectedData.Add(new FlightDataModel {
                            Departure = fl.Departure,
                            Arrival = fl.Arrival,
                            Connection = fl.Connection,
                            DepTime = dep,
                            ArrTime = arr,
                            Price = fl.CheapestFare.Price,
                            Taxes = tax
                        });
                    }
                }

                //on return fl 
                if (query.IsReturnTrip) {
                    throw new NotImplementedException("Return fl info collection not implemented");
                    // var returnFlights = ParseFlightTable(htmlDocument, "//div[@id='avaday-inbound-result']//table[@class='avadaytable']/tbody");
                    //same as dep
                    //collect taxes info
                    //write to new data
                }

                disk.SaveCollectedData(collectedData);

            } catch (Exception e) {
                System.Console.WriteLine("Error encounted while parsing date {0:yyyy/MM/dd} \"{1}\"", query.DepDate, e.Message);
                System.Console.WriteLine(e.StackTrace);
            }
        }

        private TimeSpan ParseTime(string timeString) {
            Regex timeRegex = new Regex(@"([01][0-9]|[2][0-3])[:-]([0-5][0-9])");
            Match match = timeRegex.Match(timeString);
            if (match.Success) {
                int hours = int.Parse(match.Groups[1].Value);
                int minutes = int.Parse(match.Groups[2].Value);
                return new TimeSpan(hours, minutes, 0);
            } else return TimeSpan.Zero;
        }

        private IEnumerable<NorwegianFlightData> ParseFlightTable(HtmlDocument doc, string path) {
            var result = new List<NorwegianFlightData>();
            HtmlNode table = doc.DocumentNode.SelectSingleNode(path);

            var flightInfoRows = table.SelectNodes(".//tr[contains(@class, 'rowinfo1')]")
                                        .Where(n => n.SelectSingleNode("following-sibling::tr[1]").Attributes["class"].Value.Contains("rowinfo2"))
                                        .Select(a => new {
                                            FirstRow = a,
                                            SecondRow = a.SelectSingleNode("following-sibling::tr[1]"),
                                            ThirdRow = a.SelectSingleNode("following-sibling::tr[contains(@class, 'lastrow')]")       //for connection fl
                                        });

            //collect all needed data from each fl
            foreach (var info in flightInfoRows) {
                var fares = ParseFareData(info.FirstRow);
                if (fares != null) {
                    var cheapestFare = fares.MinByPrice();

                    var arrTime = info.FirstRow.SelectSingleNode(".//td[@class='arrdest']").InnerText;
                    var depTime = info.FirstRow.SelectSingleNode(".//td[@class='depdest']").InnerText;

                    string arrival = info.SecondRow.SelectSingleNode(".//td[@class='arrdest']").InnerText;
                    string departure = info.SecondRow.SelectSingleNode(".//td[@class='depdest']").InnerText;

                    //match with "stop (time) in City | but what to do with Departure City time?
                    Match connectionMatch = Regex.Match(info.ThirdRow.InnerText, @"in (\w+)");
                    string connection = connectionMatch.Success ? connectionMatch.Groups[1].Value : "";


                    var norwegianFlightData = new NorwegianFlightData {          //could put it into FlightDataModel right away?
                        Arrival = arrival,
                        Departure = departure,
                        Connection = connection,
                        ArrTime = arrTime,
                        DepTime = depTime,
                        CheapestFare = cheapestFare
                    };
                    result.Add(norwegianFlightData);
                }
            }
            return result;
        }

        private IEnumerable<FareInfo> ParseFareData(HtmlNode fareRow) {
            var fareInfo = fareRow.SelectNodes(".//td[contains(@class, 'fareselect')]");
            var result = new List<FareInfo>();

            foreach (var fl in fareInfo) {
                string value = fl.SelectSingleNode(".//input[@value]").Attributes["value"].Value;
                string priceText = fl.SelectSingleNode(".//label[@title='EUR']").InnerText;

                decimal price;

                if (Decimal.TryParse(priceText, out price) && !value.Equals("")) {
                    var fare = new FareInfo { Price = price, Type = value };
                    result.Add(fare);
                }
            }
            return result.Count() > 0 ? result : null;
        }

        private decimal GetTaxData(HtmlDocument doc) {
            var taxNode = doc.DocumentNode.SelectNodes("//*[@id='ctl00_MainContent_ipcAvaDay_upnlResSelection']//table[1]//tr")
                                            .First(fl => fl.Descendants("span")
                                                         .Any(s => Regex.IsMatch(s.InnerText, "tax", RegexOptions.IgnoreCase)));

            Match match = Regex.Match(taxNode.InnerText, @"€(\d+[.,]\d+)");

            if (match.Success) {
                string value = match.Groups[1].Value;
                return Decimal.Parse(value);
            } else return -1;
        }
    }
}