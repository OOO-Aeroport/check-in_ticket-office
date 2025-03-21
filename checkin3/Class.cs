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
        [JsonPropertyName("flightId")]
        public int FlightId { get; set; }
        public int AirplaneID { get; set; }
        //[JsonPropertyName("departure_time")]
        public DateTime departureTime { get; set; }
        public DateTime checkinendTime { get; set; }
        public int RegistrationState { get; set; }
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

        public string ToString()
        {
            return ($"{FlightId},{RegistrationState},{seatsAvailable},{baggageAvailable}");
        }
        public bool IsSuitable(float baggageweight)
        {
            if (baggageweight <= baggageAvailable && seatsAvailable > 0) return true; //!!!
            return false;
        }
        [JsonConstructor]
        public Flight(int flightId, int airplaneId,/* DateTime departureTime,*/ int seatsAvailable, int baggageAvailable)
        {
            this.FlightId = flightId;
            this.AirplaneID = airplaneId;
            //this.departureTime = departureTime;
            this.RegistrationState = 0;
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
        public int Quantity { get; set; }

        public string ToString()
        {
            return ($"{FlightID},{Quantity}");
        }
        public BaggageInfo(int baggageQuantity, int fid)
        {
            Quantity = baggageQuantity;
            FlightID = fid;
        }
    }

    /// <summary>
    /// структура данных еды
    /// </summary>



    public class BuyRequest
    {
        [JsonPropertyName("passenger_id")]
        public int passengerId { get; set; }
        [JsonPropertyName("flight_id")]
        public int flightId { get; set; }
        [JsonPropertyName("baggage_quantity")]
        public int baggageQuantity { get; set; }

        public string ToString()
        {
            return ($"{passengerId},{flightId},{baggageQuantity}");
        }

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

        public string ToString()
        {
            return ($"{FlightID}");
        }
    }

    public class PassengerResponse
    {
        public int PassengerID { get; set; }
        public string Status { get; set; }

        public PassengerResponse(int passengerId, string status)
        {
            PassengerID = passengerId;
            Status = status;
        }

        public string ToString()
        {
            return ($"{PassengerID},{Status}");
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

        public string ToString()
        {
            return ($"{passenger_id},{flight_id}");
        }
    }

    public class UnoD
    {
        public int planeId { get; set; }

        public List<Pass> passengers { get; set; }
        public int food { get; set; }
        public int baggage { get; set; }

    }

    public class Pass 
    { 
        public int passengerId { get; set; }
    }

    public class FlightStatus
    {
        public int FlightId { get; set; }
        public bool Status { get; set; }
    }
}
