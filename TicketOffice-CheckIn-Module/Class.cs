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
        public bool Registration { get; set; }
        public string ClassPreference { get; set; }
        public string FoodPreference { get; set; }
    }
    /// <summary>
    /// Структура данных полета
    /// </summary>
    public class Flight
    {
        public int Id { get; set; }
        public char Gate { get; set; }

        public DateTime RegistrationEndTime { get; set; }
        public DateTime DepartureTime { get; set; }
        public bool IsRegistrationOpen { get; set; }
        public int AvailableSeatsEconomy { get; set; }
        public int AvailableSeatsBusiness { get; set; }

        public void ChangeSeatsByClass(string cl, int num)
        {
            if (cl == "Economy")
            {
                AvailableSeatsEconomy += num;
            }
            else if (cl == "Business")
            {
                AvailableSeatsBusiness += num;
            }
            else
            {
                if (AvailableSeatsEconomy > 0) AvailableSeatsEconomy += num;
                else if (AvailableSeatsBusiness > 0) { AvailableSeatsBusiness += num;}
            }
        }
        public bool IsSuitable(Passenger p)
        {
            if (p.ClassPreference != null) 
            {
                if (p.ClassPreference == "Economy")
                {
                    if (AvailableSeatsEconomy > 0) { return true; }
                    else { return false; }
                }
                if (p.ClassPreference == "Business")
                {
                    if (AvailableSeatsBusiness >  0) { return true; }
                    else { return false; }
                }
            }
            return true;
        }
    }

    /// <summary>
    /// структура данных билета
    /// </summary>
    public class Ticket
    {
        public int PassengerID { get; set; }
        public int FlightID { get; set; }
        public char Gate { get; set; }
        public string Class { get; set; }
        public string Food { get; set; }


        public Ticket (int pId, int flId, char g, string c, string f)
        {
            PassengerID = pId;
            FlightID = flId;
            Gate = g;
            Class = c;
            Food = f;
        }
    }

    public class RegistrationStatus
    {
        public int PassengerId { get; set; }
        public bool IsRegistered { get; set; }
    }

    /// <summary>
    /// структура данных багажа
    /// </summary>
    public class BaggageInfo
    {
        public int PassengerID { get; set; }
        public float BaggageWeight { get; set; }
        public BaggageInfo(int passengerId, float baggageWeight)
        {
            PassengerID = passengerId;
            BaggageWeight = baggageWeight;
        }
    }
    
    /// <summary>
    /// структура данных еды
    /// </summary>
    public class FoodInfo
    {
        public string FoodType { get; set; }
        public int Quantity { get; set; }
    }

    public class FoodOrder
    {
        public int FlightID { get; set; }
        public FoodInfo Food { get; set; }
    }

    public class PassengerRequest
    {
        public int PassengerID { get; set; }
        public Ticket Ticket { get; set; }

        public PassengerRequest(int passengerId, Ticket ticket)
        {
            PassengerID = passengerId;
            Ticket = ticket;
        }
    }

    public class BuyRequest
    {
        public Passenger Passenger { get; set; }
        public int FlightID { get; set; }

        public BuyRequest(Passenger p, int flightid)
        {
            Passenger = p;
            FlightID = flightid;
        }
    }

    public class RegisteredPassengersEntry
    {
        public int PassengerID { get; set; }
        public int FlightID {  get; set; }

        public RegisteredPassengersEntry(int passengerId, int flightId)
        {
            PassengerID = passengerId;
            FlightID = flightId;
        }
    }

}
