using System;
using System.Collections.Generic;
using WebScraper.Lib;

namespace WebScraper.Flysas
{
    public class SasFlightData
    {
        public string Departure { get; set; }
        public string Arrival { get; set; }
        public string Connection { get; set; }
        public TimeSpan DepTime { get; set; }
        public TimeSpan ArrTime { get; set; }

        public bool IsDirect { get => Connection.Equals(""); }
        public IEnumerable<FareInfo> Fares { get; set; }
    }
}