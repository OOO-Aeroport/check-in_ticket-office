using System.Security.Claims;
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
        public char Gate { get; set; }

        public DateTime DepartureTime { get; set; }
        public bool IsRegistrationOpen { get; set; }
        public int AvailableSeats { get; set; }
        public int AvailableBaggage { get; set; }

        public bool IsSuitable(int baggageweight)
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

    }


    public class BuyRequest
    {
        public int PassengerID { get; set; }

        public int BaggageWeight { get; set; }
        public int FlightID { get; set; }

        public BuyRequest(int passengerId, int bw, int fid)
        {
            PassengerID = passengerId;
            BaggageWeight = bw;
            FlightID = fid;
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
        public int Status { get; set; }

        public PassengerResponse(int passengerId, int ststus)
        {
            PassengerID = passengerId;
            Status = ststus;
        }

    }

    public class PassengerEntry
    {
        public int PassengerID { get; set; }
        public int FlightID {  get; set; }

        public PassengerEntry(int passengerId, int flightId)
        {
            PassengerID = passengerId;
            FlightID = flightId;
        }
    }

}
