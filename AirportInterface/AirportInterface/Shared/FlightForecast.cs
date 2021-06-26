using System;
using System.Collections.Generic;
using System.Text;

namespace AirportInterface.Shared
{
    public class FlightForecast
    {
        public string InfoProvider { get; set; }

        public string Flight_id { get; set; }

        public string Direction { get; set; }

        public String Date { get; set; }

        public string City { get; set; }

        public string Status { get; set; }

        public string Terminal { get; set; }

        public string Airline { get; set; }

        public DateTime PullDate { get; set; }
    }
}
