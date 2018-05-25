using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using WebScraper.Lib;

namespace WebScraper.Norwegian
{
    /*
    collect departure airport, arrival airport, connection airport, 
    departure time, arrival time, cheapest price and taxes 
    for all flights from OSL (Oslo) to RIX (Riga) departing from 2018-06-01 to 2018-06-30
    */

    // Data should only be collected for direct flights.

    class Program
    {
        static List<FlightDataModel> collectedData = new List<FlightDataModel>();

        static void Main(string[] args)
        {
            var client = new WebScraperClientNorwegian();

            var scrapeDate = new DateTime(2018, 6, 1);
            int days = DateTime.DaysInMonth(scrapeDate.Year, scrapeDate.Month);

            for (int d = 1; d <= days; d++)
            {
                var query = new QueryOptions
                {
                    DepDate = scrapeDate,
                    Departure = "OSL",
                    Arrival = "RIX",
                    IsDirect = true
                };
                
                if (scrapeDate.DayOfWeek != DayOfWeek.Saturday)
                {
                    client.StartScraperAsync(query).Wait();
                }
                scrapeDate = scrapeDate.AddDays(1);
            }
            System.Console.WriteLine("Data collection completed.");
            System.Console.ReadLine();
        }
    }
}