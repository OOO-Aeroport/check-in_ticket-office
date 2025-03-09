namespace TicketOffice_CheckIn_Module
{
    /// <summary>
    /// структура данных пассажира
    /// </summary>
    public class Passenger
    {
        public int Id { get; set; }
        public float BaggageWeight { get; set; }
        public bool IsDisabled { get; set; }
        public bool Registration { get; set; }
        public string FoodPreference { get; set; }
        public string ClassPreference { get; set; }
    }
    /// <summary>
    /// Структура данных полета
    /// </summary>
    public class Flight
    {
        public int Id { get; set; }
        public char Gate { get; set; }
        public bool IsRegistrationOpen { get; set; }
        public int AvailableSeatsEconomy { get; set; }
        public int AvailableSeatsBusiness { get; set; }

        public void ReduceSeatsByClass(string cl)
        {
            if (cl == "Economy")
            {
                AvailableSeatsEconomy--;
            }
            else if (cl == "Business")
            {
                AvailableSeatsBusiness--;
            }
            else
            {
                if (AvailableSeatsEconomy > 0) AvailableSeatsEconomy--;
                else if (AvailableSeatsBusiness > 0) { AvailableSeatsBusiness--;}
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
        public int PassengerId { get; set; }
        public int FlightId { get; set; }
        public char Gate { get; set; }
        public string Class { get; set; }
        public string Food { get; set; }

        public Ticket (int pId, int flId, char g, string c, string f)
        {
            PassengerId = pId;
            FlightId = flId;
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
        public int PassengerId { get; set; }
        public float BaggageWeight { get; set; }
        public BaggageInfo(int passengerId, float baggageWeight)
        {
            PassengerId = passengerId;
            BaggageWeight = baggageWeight;
        }
    }
    
    /// <summary>
    /// структура данных еды
    /// </summary>
    public class Food
    {
        public string FoodType { get; set; }
        public int Quantity { get; set; }
    }

    public class FoodOrder
    {
        public int FlightID { get; set; }
        public Food food { get; set; }
    }

    public class PassengerRegistrationRequest
    {
        public int PassengerId { get; set; }
        public Ticket Ticket { get; set; }

        public PassengerRegistrationRequest(int passengerId, Ticket ticket)
        {
            PassengerId = passengerId;
            Ticket = ticket;
        }
    }

    public class RegisteredPassengersEntry
    {
        public int PassengerId { get; set; }
        public string Name { get; set; }
        public int FlightID {  get; set; }
        public string Class {  get; set; }

        public RegisteredPassengersEntry(int passengerId, string name, int flightId, string cl )
        {
            PassengerId = passengerId;
            Name = name;
            FlightID = flightId;
            Class = cl;
        }
    }
}
