using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace TicketOffice_CheckIn_Module
{
    /// <summary>
    /// структура данных пассажира
    /// </summary>
    public class Passenger
    {
        public int Id { get; set; }
        public float BaggageWeight { get; set; }
    }
    /// <summary>
    /// Структура данных полета
    /// </summary>
    public class Flight
    {
        public int Id { get; set; }
        public string DepartureTime { get; set; }
        public bool IsRegistrationOpen { get; set; }
        public int AvailableSeats { get; set; }
        public int AvailableBaggage { get; set; }

        public DateTime GetDateTime()
        {
            string[]dts = DepartureTime.Split(':');
            string shours = dts[0];
            int.TryParse(shours, out int hours);
            string sminutes = dts[1];
            int.TryParse(shours, out int minutes);
            DateTime res = new DateTime(0,0,0,hours,minutes,0,0,0);
            return res;
        }

        public bool IsSuitable(float baggageweight)
        {
            if (baggageweight >= AvailableBaggage && AvailableSeats > 0) return true;
            return false;
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
        public int FlightID { get; set; }
        public int Quantity { get; set; }
        public FoodOrder(int fid) 
        { 
            FlightID = fid; 
            Quantity = 0; 
        }

    }


    public class BuyRequest
    {
        public int PassengerID { get; set; }

        public float BaggageWeight { get; set; }
        public int FlightID { get; set; }

        [JsonConstructor]
        public BuyRequest(int passengerID, float baggageWeight, int flightID)
        {
            PassengerID = passengerID;
            BaggageWeight = baggageWeight;
            FlightID = flightID;
        }
    }

    public class FlightInfo
    {
        public int FlightID { get; set; }
        public string CheckinStart { get; set; }
        public string DepartureTime { get; set;}
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
        public int PassengerID { get; set; }
        public int FlightID {  get; set; }
        [JsonConstructor]
        public PassengerEntry(int passengerID, int flightID)
        {
            PassengerID = passengerID;
            FlightID = flightID;
        }
    }

}
