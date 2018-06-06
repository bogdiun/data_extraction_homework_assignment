using System;
using WebScraper.Lib;

namespace WebScraper.Flysas
{

    class Program
    {

        static void Main(string[] args)
        {
            var query = new QueryOptions
            {
                Departure = "ARN",
                Arrival = "LHR",
                DepDate = new DateTime(2018, 7, 4),
                RetDate = new DateTime(2018, 7, 10)
            };

            var client = new WebScraperClientFlysas();
            client.StartScraperAsync(query).Wait();
            // var webDriver = new WebDriverFlysas();
            // webDriver.StartScrape(query);

            System.Console.WriteLine("Data Collection Completed");
            Console.ReadLine();
        }
    }
}
