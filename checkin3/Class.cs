using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace TicketOffice_CheckIn_Module
{

    /// <summary>
    /// Структура данных полета
    /// </summary>
    public class Flight
    {
        [JsonPropertyName("id")]
        public int id { get; set; }
        //[JsonPropertyName("departure_time")]
        public DateTime departureTime { get; set; }
        public bool IsRegistrationOpen { get; set; }
        //[JsonPropertyName("seats_available")]
        public int seatsAvailable { get; set; }
        //[JsonPropertyName("baggage_available")]
        public int baggageAvailable { get; set; }

        //public DateTime GetDateTime()
        //{
        //    string[]dts = DepartureTime.Split(':');
        //    string shours = dts[0];
        //    int.TryParse(shours, out int hours);
        //    string sminutes = dts[1];
        //    int.TryParse(shours, out int minutes);
        //    DateTime res = new DateTime(0,0,0,hours,minutes,0,0,0);
        //    return res;
        //}

        public bool IsSuitable(float baggageweight)
        {
            if (baggageweight <= baggageAvailable && seatsAvailable > 0) return true; //!!!
            return false;
        }
        [JsonConstructor]
        public Flight(int id, DateTime departureTime, bool isRegistrationOpen, int seatsAvailable, int baggageAvailable)
        {
            this.id = id;
            this.departureTime = departureTime;
            this.IsRegistrationOpen = isRegistrationOpen;
            this.seatsAvailable = seatsAvailable;
            this.baggageAvailable = baggageAvailable;
        }
    }


    /// <summary>
    /// структура данных багажа
    /// </summary>
    public class BaggageInfo
    {
        public int FlightID { get; set; }
        public int PassengerID { get; set; }
        public float BaggageWeight { get; set; }
        public BaggageInfo(int passengerId, float baggageWeight, int fid)
        {
            PassengerID = passengerId;
            BaggageWeight = baggageWeight;

        }
    }

    /// <summary>
    /// структура данных еды
    /// </summary>


    public class FoodOrder
    {
        public int flight_id { get; set; }
        public int quantity { get; set; }
        public FoodOrder(int fid)
        {
            flight_id = fid;
            quantity = 0;
        }

    }


    public class BuyRequest
    {
        [JsonPropertyName("passenger_id")]
        public int passenger_id { get; set; }
        [JsonPropertyName("flight_id")]
        public int flight_id { get; set; }
        [JsonPropertyName("baggage_weight")]

        public int baggage_weight { get; set; }

    }

    public class FlightInfo
    {
        public int FlightID { get; set; }
        public string CheckinStart { get; set; }
        public string DepartureTime { get; set; }
        public FlightInfo(int flightID, string checkinStart, string departureTime)
        {
            FlightID = flightID;
            CheckinStart = checkinStart;
            DepartureTime = departureTime;
        }
    }

    public class PassengerResponse
    {
        public int PassengerID { get; set; }
        public string Status { get; set; }

        public PassengerResponse(int passengerId, string ststus)
        {
            PassengerID = passengerId;
            Status = ststus;
        }

    }

    public class PassengerEntry
    {
        public int passenger_id { get; set; }
        public int flight_id { get; set; }
        [JsonConstructor]
        public PassengerEntry(int passenger_id, int flight_id)
        {
            this.passenger_id = passenger_id;
            this.flight_id = flight_id;
        }
    }


}
