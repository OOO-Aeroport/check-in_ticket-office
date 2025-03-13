using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using TicketOffice_CheckIn_Module;


var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

//URL �������
string DepartureBoardUrl = "";
string CateringServiceUrl = "";
string LuggageServiceUrl = "";
string PassengerModuleUrl = "";


// ��������� ������
List<PassengerEntry> RegisteredPassengers = new List<PassengerEntry>();
List<int> BuyerIDs = new List<int>();
List<Flight> Flights = new List<Flight>();
List<BaggageInfo> Baggage = new List<BaggageInfo>();
List<FoodOrder> foodOrders= new List<FoodOrder>();

// ��������� �������� ������� ���������
async Task<DateTime> GetSimulationTime()
{
    using (var httpClient = new HttpClient())
    {
        string departureBoardUrl = $"http://{DepartureBoardUrl}/departure-board/time";
        try
        {
            var response = await httpClient.GetAsync(departureBoardUrl);
            if (response.IsSuccessStatusCode)
            {
                var responseData = await response.Content.ReadAsStringAsync();
                return DateTime.Parse(responseData);
            }
            else
            {
                Console.WriteLine($"Departure board request error: {response.StatusCode}");
                throw new Exception("Unable to get simulation time.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Departure board request error: {ex.Message}");
            throw;
        }
    }
}

// �������� ���������� ����������� ��� ���� ������ 
async Task CheckRegistrationStatus()
{
    var simulationTime = await GetSimulationTime();
    foreach (var flight in Flights)
    {
        if (!flight.IsRegistrationOpen && simulationTime >= flight.DepartureTime.AddHours(-3))
        {
            // ��������� �����������
            flight.IsRegistrationOpen = true;
            Console.WriteLine($"Check-in for flight {flight.Id} is open.");
        }
        if (flight.IsRegistrationOpen && simulationTime >= flight.DepartureTime.AddMinutes(-30))
        {
            // ��������� �����������
            flight.IsRegistrationOpen = false;
            Console.WriteLine($"Check-in for flight {flight.Id} closed.");

            // ���������� ������ � �����, ������ ������� � ������ ������
            await SendRegistrationCompletionData(flight);
        }
    }
}

// �������� ������ � ���������� �����������
async Task SendRegistrationCompletionData(Flight flight)
{
    // �������� ������ ������������������ ����������
    var registeredPassengers = GetRegisteredPassengersByFlight(flight.Id);
    // �������� ������ ������� ���
    var foodOrdersForFlight = GetFoodOrderByFlight(flight.Id); 
    // �������� ������ ������
    var baggageForFlight = GetBaggageByFlight(flight.Id); 


    // ���������� ������ � �����
    string departureBoardUrl = $"http://{DepartureBoardUrl}/registration-completion";
    await SendDataToService(departureBoardUrl, new { FlightID = flight.Id, Passengers = registeredPassengers });
    Console.WriteLine($"Departure table data sent successfully.");

    // ���������� ������ � ������ �������
    string cateringServiceUrl = $"http://{CateringServiceUrl}/food-orders";
    await SendDataToService(cateringServiceUrl, new { FlightID = flight.Id, FoodOrders = foodOrdersForFlight });
    Console.WriteLine($"Catering Service data sent successfully.");

    // ���������� ������ � ������ ������
    string luggageServiceUrl = $"http://{LuggageServiceUrl}/transportation-bagg";
    await SendDataToService(luggageServiceUrl, new { FlightID = flight.Id, Baggage = baggageForFlight });
    Console.WriteLine($"Luggage Service data sent successfully.");
}

// ����� ����� ��� �������� ������
async Task SendDataToService(string url, object data)
{
    using (var httpClient = new HttpClient())
    {
        var jsonData = JsonSerializer.Serialize(data);
        var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(url, content);
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Data sent successfully to {url}");
        }
        else
        {
            Console.WriteLine($"Error sending data to {url}: {response.StatusCode}");
        }
    }
}

//�������� ����� ���
void AddFoodOrder(int id)
{
    for (int i = 0; i < foodOrders.Count; i++)
    {
        if (id == foodOrders[i].FlightID)
        {
            foodOrders[i].Quantity++;
        }
    }
}

//����� ������������������ ���������� �� �����
List<PassengerEntry> GetRegisteredPassengersByFlight(int flightID)
{
    List<PassengerEntry> res = new List<PassengerEntry>();
    for (int i = 0; i < RegisteredPassengers.Count; i++)
    {
        if (RegisteredPassengers[i].FlightID == flightID) res.Add(RegisteredPassengers[i]);
    }
    if (res.Count > 0) Console.WriteLine($"Found RegisteredPassengers for flight {flightID}");
    return res;
}

bool IfBuyer(int passid)
{
    for(int i = 0;i < BuyerIDs.Count;i++) { if (BuyerIDs[i] == passid) return true; }
    return false;
}

FoodOrder GetFoodOrderByFlight(int flightID)
{
    FoodOrder res = new FoodOrder();
    for (int i = 0; i < foodOrders.Count; i++)
    {
        if (foodOrders[i].FlightID == flightID) res = foodOrders[i];
        Console.WriteLine($"Found food order for flight {flightID}");
        return res;
    }
    Console.WriteLine($"Couldn't find food order for flight {flightID}");
    return res;
}

//��������� ����� �� ������ �� ID
Flight GetFlightByID(int id, List<Flight> Flights)
{
    for (int i = 0; i < Flights.Count; i++)
    {
        if (Flights[i].Id == id) return Flights[i];
    }
    return null;
}

List<BaggageInfo> GetBaggageByFlight(int flightID)
{
    List<BaggageInfo> res = null;
    foreach (var bg in Baggage)
    {
        if (bg.FlightID == flightID)
        {
            res.Add(bg);
            break;
        }
    }
    return res;
}

// ������� ������
app.MapPost("/ticket-office/buy-ticket", async context =>
{
    var request = await context.Request.ReadFromJsonAsync<List<BuyRequest>>();
    PassengerResponse resp;
    if (request == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
       Console.WriteLine("Invalid request data.");
        resp = new PassengerResponse(-1, StatusCodes.Status404NotFound);
        return;
    }
    else
    {
        foreach (var psg in request)
        {
            var flight = GetFlightByID(psg.FlightID, Flights);
            if (flight == null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                Console.WriteLine("Impossible to buy a ticket for this flight.");
                resp = new PassengerResponse(psg.PassengerID, StatusCodes.Status404NotFound);
                return;
            }

            // �������� ������� �������
            var simulationTime = await GetSimulationTime();
            if (simulationTime >= flight.DepartureTime.AddHours(-3)) // ������� ������������� �� 3 ���� �� ������
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                Console.WriteLine("Ticket sale for this flight is closed.");
                resp = new PassengerResponse(psg.PassengerID, StatusCodes.Status404NotFound);
                return;
            }
            if (!flight.IsSuitable(psg.BaggageWeight))
            {
                Console.WriteLine("The flight is unsuitable.");
                resp = new PassengerResponse(psg.PassengerID, StatusCodes.Status404NotFound);
                return;
            }

            // ������� �����
            Baggage.Add(new BaggageInfo(psg.PassengerID, psg.BaggageWeight, flight.Id));
            flight.AvailableSeats--;
            resp = new PassengerResponse(psg.PassengerID, StatusCodes.Status200OK);

            // �������� ������ �� ������ ���������
            string passengerModuleUrl = $"http://{PassengerModuleUrl}/passenger/ticket";
            await SendPurchaseStatus(resp, passengerModuleUrl);

            context.Response.StatusCode = StatusCodes.Status200OK;
            Console.WriteLine($"Ticket for passenger {psg.PassengerID} sold successfully. Seats left: {flight.AvailableSeats}");
        }
    }
});

//�������� ������� �������
async Task SendPurchaseStatus(PassengerResponse r, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        // ������� ������ � ID ���������
        var purchaseData = new { PassengerId = r.PassengerID, Status = r.Status };
        var purchaseJson = JsonSerializer.Serialize(purchaseData);
        var content = new StringContent(purchaseJson, Encoding.UTF8, "application/json");

        // ���������� POST-������
        var response = await httpClient.PostAsync(passengerModuleUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"�������� {r.PassengerID} ������� ����� �����.");
        }
        else
        {
            Console.WriteLine($"������: {response.StatusCode}");
        }
    }
}

// ����������� ���������
app.MapPost("/check-in/passenger", async context =>
{
    PassengerResponse resp;
    var request = await context.Request.ReadFromJsonAsync<List<PassengerEntry>>();
    if (request == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        Console.WriteLine("Invalid passenger data.");
        resp = new PassengerResponse(-1, StatusCodes.Status404NotFound);
        return;
    }
    foreach (var psg in request)
    {
        var passengerId = psg.PassengerID;
        var flight = GetFlightByID(psg.FlightID, Flights);

        if (flight == null || !flight.IsRegistrationOpen)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            Console.WriteLine("Check-in for the flight is closed or the flight doesn't exist.");
            resp = new PassengerResponse(psg.PassengerID, StatusCodes.Status404NotFound);
            return;
        }

        // �������� ������� �����������
        var simulationTime = await GetSimulationTime();
        if (simulationTime >= flight.DepartureTime.AddMinutes(-30)) // ����������� ������������� � ��������� �����
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            Console.WriteLine("Check-in for the flight is closed.");
            resp = new PassengerResponse(psg.PassengerID, StatusCodes.Status404NotFound);
            return;
        }

        // ������������ ���������
        RegisteredPassengers.Add(psg);
        AddFoodOrder(flight.Id);

        // ���������� ID ��������� � ������ ��������� ��� ���������� �������
        string passengerModuleUrl = $"http://{PassengerModuleUrl}/passenger/registration";
        resp = new PassengerResponse(psg.PassengerID, StatusCodes.Status200OK);
        await SendPassengerRegistrationStatus(resp, passengerModuleUrl);

        context.Response.StatusCode = StatusCodes.Status200OK;
        Console.WriteLine($"Passenger {passengerId} registered successfully.");
    }
});

//�������� ������� ������������������� ���������
async Task SendPassengerRegistrationStatus(PassengerResponse p, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        // ������� ������ � ID ���������
        var registrationData = new { PassengerId = p.PassengerID, Status = p.Status };
        var registrationJson = JsonSerializer.Serialize(registrationData);
        var content = new StringContent(registrationJson, Encoding.UTF8, "application/json");

        // ���������� POST-������
        var response = await httpClient.PostAsync(passengerModuleUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Passenger status {p.PassengerID} updated successfully 'Registered'.");
        }
        else
        {
            Console.WriteLine($"Error updating passenger status: {response.StatusCode}");
        }
    }
}


// ������� ������
app.MapPost("/ticket-office/return-ticket", async context =>
{
    PassengerResponse resp;
    var request = await context.Request.ReadFromJsonAsync<List<PassengerEntry>>();
    if (request == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        Console.WriteLine("Invalid request data.");
        resp = new PassengerResponse(-1, StatusCodes.Status404NotFound);
        return;
    }
    foreach (var psg in request)
    {
        if (!IfBuyer(psg.PassengerID))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            Console.WriteLine("Ticket not found or forged.");
            resp = new PassengerResponse(psg.PassengerID, StatusCodes.Status404NotFound);
            return;
        }

        var flight = GetFlightByID(psg.FlightID, Flights);
        if (flight == null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            Console.WriteLine("Flight not found.");
            resp = new PassengerResponse(psg.PassengerID, StatusCodes.Status404NotFound);
            return;
        }

        var simulationTime = await GetSimulationTime();
        if (simulationTime >= flight.DepartureTime.AddHours(-3)) // ������� �� 3 ���� �� ������
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            Console.WriteLine("Unable to return ticket: less than 3 hours before departure.");
            resp = new PassengerResponse(psg.PassengerID, StatusCodes.Status404NotFound);
            return;
        }

        // ������� ������
        BuyerIDs.Remove(psg.PassengerID);
        flight.AvailableSeats++;
        string returnModuleUrl = $"http://{PassengerModuleUrl}/passenger/available-flights";
        resp = new PassengerResponse(psg.PassengerID, StatusCodes.Status200OK);
        await SendReturnStatus(resp, returnModuleUrl);
        context.Response.StatusCode = StatusCodes.Status200OK;
        Console.WriteLine($"Ticket for passenger {psg.PassengerID} returned successfully.");
    }
});

//�������� ������� ���������, ������������� �����
async Task SendReturnStatus(PassengerResponse p, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        // ������� ������ � ID ���������
        var registrationData = new { PassengerId = p.PassengerID, Status = p.Status };
        var registrationJson = JsonSerializer.Serialize(registrationData);
        var content = new StringContent(registrationJson, Encoding.UTF8, "application/json");

        // ���������� POST-������
        var response = await httpClient.PostAsync(passengerModuleUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Passenger {p.PassengerID} returned the ticket successfully.");
        }
        else
        {
            Console.WriteLine($"Error returning ticket: {response.StatusCode}");
        }
    }
}


List<FlightInfo>GetAvailableFlights(DateTime curr)
{
    List<FlightInfo> res = new List<FlightInfo>();
    foreach (var flight in Flights)
    {
        if (curr <= flight.DepartureTime.AddHours(-3)) res.Add(new FlightInfo(flight.Id, flight.DepartureTime.AddHours(-3), flight.DepartureTime));
    }
    return res;
}

/// �������� ��� ������ ������ ����� �������� �������
app.MapGet("/ticket-office/available-flights", async context =>
{
    var simulationTime = await GetSimulationTime();
    List<FlightInfo> availableFlights = GetAvailableFlights(simulationTime); //������ ������ �� ������� ����� ������ ������

    await context.Response.WriteAsJsonAsync(availableFlights);
});

/// ��������� ��������
app.MapGet("/", async context =>
{
    Console.WriteLine("Welcome to the Ticket Office / Check-In module!");
    await context.Response.WriteAsync("Welcome to the Ticket Office / Check-In module!");
});

// �������� ��� ��������� ����� ������ �� �����
app.MapPost("ticket-office/flights", async context =>
{
    var newFlight = await context.Request.ReadFromJsonAsync<Flight>();
    if (newFlight == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        Console.WriteLine("Invalid flight data.");
        return;
    }

        var existingFlight = Flights.FirstOrDefault(f => f.Id == newFlight.Id);
        if (existingFlight == null)
        {
            Flights.Add(newFlight);
            Console.WriteLine($"New flight added: ID {newFlight.Id}, Departure: {newFlight.DepartureTime}");
        }
        else
        {
            // ��������� ������������ ���� (���� �����)
            existingFlight.DepartureTime = newFlight.DepartureTime;
            existingFlight.IsRegistrationOpen = newFlight.IsRegistrationOpen;
            existingFlight.AvailableSeats = newFlight.AvailableSeats;
            Console.WriteLine($"Flight updated: ID {newFlight.Id}");
        }

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync("Flights updated successfully.");
});

var timer = new System.Timers.Timer(1000); // �������� ������ ��������� ���������
timer.Elapsed += async (sender, e) => await CheckRegistrationStatus();
timer.Start();

app.Run();