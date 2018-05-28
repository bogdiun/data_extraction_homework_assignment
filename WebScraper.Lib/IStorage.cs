using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WebScraper.Lib
{
    public interface IStorage
    {
        void SaveCollectedData(Object data);
    }

    public class FlightDataFileStorage : IStorage
    {
        public string path { get; set; } = "data";

        public void SaveCollectedData(Object data)
        {
            var flightData = data as IEnumerable<FlightDataModel>;

            Directory.CreateDirectory(path);

            using (var fileStream = new FileStream($@"{path}/collectedData.txt", FileMode.Append))
            using (var file = new StreamWriter(fileStream))
            {
                foreach (var fl in flightData)
                {
                    file.WriteLine(" ---- Flight separator ----\n");
                    file.WriteLine($"from: {fl.Departure}");
                    file.WriteLine($"to: {fl.Arrival}");

                    if (fl.Connection != "")
                    {
                        file.WriteLine($"connected: {fl.Connection}\n");
                    }
                    else file.WriteLine();

                    file.WriteLine($"departure time: {fl.DepTime}");
                    file.WriteLine($"arrival time: {fl.ArrTime}\n");
                    file.WriteLine($"total price: {fl.Price}");
                    file.WriteLine($"taxes: {fl.Taxes}\n");
                }
                file.WriteLine("------------------------");
            }
        }

    }

    public class RoundTripDataFileStorage : IStorage
    {
        public string path { get; set; } = "data";

        public void SaveCollectedData(Object data)
        {
            var roundTripData = data as IEnumerable<RoundTripFlightData>;

            Directory.CreateDirectory(path);

            using (var fileStream = new FileStream($@"{path}/collectedData_{DateTime.Now.Ticks}.txt", FileMode.Create))
            using (var file = new StreamWriter(fileStream))
            {
                foreach (var fl in roundTripData)
                {
                    file.WriteLine("----- Flight separator -----\n");
                    file.WriteLine($"from: {fl.Outbound.Departure}");
                    file.WriteLine($"to: {fl.Outbound.Arrival}");

                    if (fl.Outbound.Connection != "")
                    {
                        file.WriteLine($"connected: {fl.Outbound.Connection}\n");
                    }
                    else file.WriteLine("");

                    file.WriteLine($"departure time: {fl.Outbound.DepTime}");
                    file.WriteLine($"arrival time: {fl.Outbound.ArrTime}\n");

                    file.WriteLine($"return_from: {fl.Inbound.Departure}");
                    file.WriteLine($"return_to: {fl.Inbound.Arrival}");
                    if (fl.Inbound.Connection != "")
                    {
                        file.WriteLine($"connected: {fl.Inbound.Connection}\n");
                    }
                    else file.WriteLine();

                    file.WriteLine($"departure time: {fl.Inbound.DepTime}");
                    file.WriteLine($"arrival time: {fl.Inbound.ArrTime}\n");
                    file.WriteLine($"total price: {fl.Outbound.Price + fl.Inbound.Price}€({fl.Outbound.Price}€+{fl.Inbound.Price}€)");
                    file.WriteLine($"taxes: {fl.Outbound.Taxes + fl.Inbound.Taxes}\n\n");   //no success here
                }
                file.WriteLine("------------------------");
            }
        }
    }
}