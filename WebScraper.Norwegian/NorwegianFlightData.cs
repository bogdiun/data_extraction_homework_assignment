using WebScraper.Lib;

namespace WebScraper.Norwegian {

    public class NorwegianFlightData {
        public string DepTime { get; set; }
        public string ArrTime { get; set; }
        public string Departure { get; set; }
        public string Arrival { get; set; }
        public string Connection { get; set; }
        public FareInfo CheapestFare { get; set; }
    }
}
