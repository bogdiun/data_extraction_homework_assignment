using System;
using System.Collections.Generic;
using System.Linq;

namespace WebScraper.Lib
{
    public class QueryOptions
    {
        public string Departure { get; set; }
        public string Arrival { get; set; }

        public bool IsDirect { get; set; }
        public bool IsRoundTrip => RetDate > DepDate ? true : false;

        public DateTime DepDate { get; set; }
        public string DepFlight { get; set; }
        public string DepFareType { get; set; }

        public DateTime RetDate { get; set; }
        public string RetFlight { get; set; }
        public string RetFareType { get; set; }

        public string ConvertFormDataToQuery(Dictionary<string, string> form) =>
            form.Select(pair => $"{pair.Key}={pair.Value}").Aggregate((a, b) => a + "&" + b);
    }
}