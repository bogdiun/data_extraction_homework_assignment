using System;

namespace WebScraper.Lib
{

    public class FlightDataModel
    {
        public string Departure { get; set; }
        public string Arrival { get; set; }
        public string Connection { get; set; }
        public DateTime DepTime { get; set; }
        public DateTime ArrTime { get; set; }
        public decimal Price { get; set; }
        public decimal Taxes { get; set; }
    }

    public class FareInfo
    {
        public decimal Price { get; set; } = 0;
        public decimal Taxes { get; set; } = 0;
        public string Type { get; set; } = "";
        public string Id { get; set; } = "";
    }

    public class RoundTripFlightData
    {
        public FlightDataModel Outbound { get; set; }
        public FlightDataModel Inbound { get; set; }
    }
}
