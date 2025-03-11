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

// ��������� ������
List<RegisteredPassengersEntry> RegisteredPassengers = new List<RegisteredPassengersEntry>();
List<Passenger> Buyers = new List<Passenger>();
List<Flight> Flights = new List<Flight>();
List<BaggageInfo> Baggage = new List<BaggageInfo>();
List<FoodOrder> foodOrders= new List<FoodOrder>();

// ����� ���������
DateTime simulationStartTime = DateTime.MinValue; // ����� ������� ������� ���������
bool isSimulationTimeSet = false;

// ������ ����� ������� ������� � ����� ��� ������ ������
async Task InitializeSimulationTime()
{
    using (var httpClient = new HttpClient())
    {
        string departureBoardUrl = "http://departure-board-url/simulation-start-time";
        try
        {
            var response = await httpClient.GetAsync(departureBoardUrl);
            if (response.IsSuccessStatusCode)
            {
                var responseData = await response.Content.ReadAsStringAsync();
                simulationStartTime = DateTime.Parse(responseData);
                isSimulationTimeSet = true;
                Console.WriteLine($"����� ������� ������� ��������� �����������: {simulationStartTime}");
            }
            else
            {
                Console.WriteLine($"������ ��� ������� ������� � �����: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"������ ��� ������� ������� � �����: {ex.Message}");
        }
    }
}

// ������������� ������� ��������� ��� ������ ������
await InitializeSimulationTime();

// ��������� �������� ������� ���������
DateTime GetSimulationTime()
{
    if (!isSimulationTimeSet)
    {
        throw new InvalidOperationException("Simulation time has not been set.");
    }

    var realTimeElapsed = DateTime.Now - simulationStartTime;
    var simulationTimeElapsed = realTimeElapsed.TotalMinutes / 2; // 2 ������ ��������� ������� = 1 ��� ���������
    return simulationStartTime.AddHours(simulationTimeElapsed);
}

// �������� ���������� ����������� ��� ���� ������
async Task CheckRegistrationCompletion()
{
    var simulationTime = GetSimulationTime();
    foreach (var flight in Flights)
    {
        if (flight.IsRegistrationOpen && simulationTime >= flight.RegistrationEndTime)
        {
            // ��������� �����������
            flight.IsRegistrationOpen = false;
            Console.WriteLine($"����������� �� ���� {flight.Id} ���������.");

            // ���������� ������ � �����, ������ ������� � ������ ������
            await SendRegistrationCompletionData(flight);
        }
    }
}

// �������� ������ � ���������� �����������
async Task SendRegistrationCompletionData(Flight flight)
{
    // �������� ������ ������������������ ����������
    var registeredPassengers = RegisteredPassengers
        .Where(p => p.FlightID == flight.Id)
        .Select(p => new { p.PassengerID })
        .ToList();

    // �������� ������ ������� ���
    var foodOrdersForFlight = foodOrders
        .Where(f => f.FlightID == flight.Id)
        .ToList();

    // �������� ������ ������
    var baggageForFlight = Baggage
        .Where(b => registeredPassengers.Any(p => p.PassengerID == b.PassengerID))
        .Select(b => new { b.PassengerID, b.BaggageWeight })
        .ToList();

    // ���������� ������ � �����
    string departureBoardUrl = "http://departure-board-url/registration-completion";
    await SendDataToService(departureBoardUrl, new { FlightID = flight.Id, Passengers = registeredPassengers });

    // ���������� ������ � ������ �������
    string cateringServiceUrl = "http://catering-service-url/food-orders";
    await SendDataToService(cateringServiceUrl, new { FlightID = flight.Id, FoodOrders = foodOrdersForFlight });

    // ���������� ������ � ������ ������
    string luggageServiceUrl = "http://luggage-service-url/baggage-info";
    await SendDataToService(luggageServiceUrl, new { FlightID = flight.Id, Baggage = baggageForFlight });
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
            Console.WriteLine($"������ ������� ���������� �� {url}");
        }
        else
        {
            Console.WriteLine($"������ ��� �������� ������ �� {url}: {response.StatusCode}");
        }
    }
}


//�������� ����� ���
void AddFoodOrder(int id, string food)
{
    for (int i = 0; i < foodOrders.Count; i++)
    {
        if (id == foodOrders[i].FlightID)
        {
            if (food == foodOrders[i].Food.FoodType) foodOrders[i].Food.Quantity++;
        }
    }
}

//����� ������������������ ���������� �� �����
List<RegisteredPassengersEntry> GetRegisteredPassengersByFlight(int flightID)
{
    List<RegisteredPassengersEntry> res = new List<RegisteredPassengersEntry>();
    for (int i = 0; i < RegisteredPassengers.Count; i++)
    {
        if (RegisteredPassengers[i].FlightID == flightID) res.Add(RegisteredPassengers[i]);
    }
    return res;
}

Passenger GetPassengerByID(int id, List<Passenger> Passengers)
{
    for (int i = 0; i < Passengers.Count; i++)
    {
        if (Passengers[i].Id == id) return Passengers[i];
    }
    return null;
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

// ����������� ���������
app.MapPost("/check-in/passenger", async context =>
{
    var request = await context.Request.ReadFromJsonAsync<PassengerRequest>();
    if (request == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Invalid passenger data.");
        return;
    }

    var passengerId = request.PassengerID;
    var ticket = request.Ticket;
    var flight = GetFlightByID(ticket.FlightID, Flights);

    if (ticket == null || flight == null || !flight.IsRegistrationOpen)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("����������� �� ���� ������� ��� ���� �� ����������.");
        return;
    }

    // �������� ������� �����������
    var simulationTime = GetSimulationTime();
    if (simulationTime >= flight.RegistrationEndTime) // ����������� ������������� � ��������� �����
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("����������� �� ���� ��� �������.");
        return;
    }

    // ������� ���������
    var passenger = new RegisteredPassengersEntry(passengerId, ticket.FlightID);
    RegisteredPassengers.Add(passenger);

    // ���������� ID ��������� � ������ ��������� ��� ���������� �������
    string passengerModuleUrl = "http://passenger-module-url/passenger/registration";
    await SendPassengerRegistrationStatus(passengerId, passengerModuleUrl);

    // ��������� �����
    Baggage.Add(new BaggageInfo(passengerId, GetPassengerByID(passengerId, Buyers).BaggageWeight));

    AddFoodOrder(flight.Id, ticket.Food);

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync($"�������� {passengerId} ������� ���������������.");
});

//�������� ������� ������������������� ���������
async Task SendPassengerRegistrationStatus(int passengerId, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        // ������� ������ � ID ���������
        var registrationData = new { PassengerId = passengerId };
        var registrationJson = JsonSerializer.Serialize(registrationData);
        var content = new StringContent(registrationJson, Encoding.UTF8, "application/json");

        // ���������� POST-������
        var response = await httpClient.PostAsync(passengerModuleUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"������ ��������� {passengerId} ������� �������� �� '���������������'.");
        }
        else
        {
            Console.WriteLine($"������ ��� ���������� ������� ���������: {response.StatusCode}");
        }
    }
}

// ������� ������
app.MapPost("/ticket-office/return-ticket", async context =>
{
    var request = await context.Request.ReadFromJsonAsync<PassengerRequest>();
    if (request == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Invalid request data.");
        return;
    }

    var byer = Buyers.FirstOrDefault(t => t.Id == request.PassengerID);
    if (byer == null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("����� �� ������ ��� �������� ����������.");
        return;
    }

    var flight = GetFlightByID(request.Ticket.FlightID, Flights);
    if (flight == null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("���� �� ������.");
        return;
    }

    var simulationTime = GetSimulationTime();
    if (simulationTime >= flight.DepartureTime.AddHours(-3)) // ������� �� 3 ���� �� ������
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("����� ������ �������: �������� ����� 3 ����� �� ������.");
        return;
    }

    // ������� ������
    Buyers.Remove(byer);
    flight.ChangeSeatsByClass(request.Ticket.Class, 1);

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync($"����� ��� ��������� {request.PassengerID} ������� ���������.");
});


// �������� ������ ������������������ ���������� � �����
async Task SendRegisteredPassengers(List<RegisteredPassengersEntry> passengers, string departureBoardUrl)
{
    using (var httpClient = new HttpClient())
    {
        // ������������ ������ ���������� � JSON
        var passengersJson = JsonSerializer.Serialize(passengers);
        var content = new StringContent(passengersJson, Encoding.UTF8, "application/json");

        // �������� POST-�������
        var response = await httpClient.PostAsync(departureBoardUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("������ ������������������ ���������� ������� ��������� �� ����� ������.");
        }
        else
        {
            Console.WriteLine($"������ ��� �������� ������ ����������: {response.StatusCode}");
        }
    }
}


// �������� ���������� � ������ � ������ ������
async Task SendBaggageInfo(List<BaggageInfo> luggageList, string luggageinfourl)
{
    using (var httpClient = new HttpClient())
    {
        // ������������ ������ ������� ������� � JSON
        var foodOrdersJson = JsonSerializer.Serialize(foodOrders);
        var content = new StringContent(foodOrdersJson, Encoding.UTF8, "application/json");

        // �������� POST-�������
        var response = await httpClient.PostAsync(luggageinfourl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("���������� � ������ ������� ���������� � ������ ������.");
        }
        else
        {
            Console.WriteLine($"������ ��� �������� ���������� � ������: {response.StatusCode}");
        }
    }
}

// �������� ������� ��� � ������ ���
async Task SendFoodOrderToCateringService(List<FoodOrder> foodOrders, string cateringServiceUrl)
{
    using (var httpClient = new HttpClient())
    {
        // ������������ ������ ������� ������� � JSON
        var foodOrdersJson = JsonSerializer.Serialize(foodOrders);
        var content = new StringContent(foodOrdersJson, Encoding.UTF8, "application/json");

        // �������� POST-�������
        var response = await httpClient.PostAsync(cateringServiceUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("���������� � ������� ������� ���������� � ������ �������.");
        }
        else
        {
            Console.WriteLine($"������ ��� �������� ���������� � �������: {response.StatusCode}");
        }
    }
}

// ������� ������
app.MapPost("/ticket-office/buy-ticket", async context =>
{
    var request = await context.Request.ReadFromJsonAsync<List<BuyRequest>>();
    if (request == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Invalid request data.");
        return;
    }
    else
    {
        foreach (var psg in request)
        {
            var flight = GetFlightByID(psg.FlightID, Flights);
            if (flight == null || !flight.IsRegistrationOpen)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("���������� ������ ����� �� ������ ����.");
                return;
            }

            // �������� ������� �������
            var simulationTime = GetSimulationTime();
            if (simulationTime >= flight.DepartureTime.AddHours(-3)) // ������� ������������� �� 3 ���� �� ������
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("������� ������� �� ������ ���� �������.");
                return;
            }

            // ������� �����
            var ticket = new Ticket(psg.Passenger.Id, flight.Id, flight.Gate, psg.Passenger.ClassPreference, psg.Passenger.FoodPreference);
            Buyers.Add(psg.Passenger);
            flight.ChangeSeatsByClass(ticket.Class, -1);

            // �������� ������ �� ������ ���������
            string passengerModuleUrl = "http://other-module-url/passenger/ticket";
            await SendTicketToPassengerModule(ticket, passengerModuleUrl);

            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync($"����� ��� ��������� {psg.Passenger.Id} ������� ������. �������� ����: \n ������ �����: {flight.AvailableSeatsEconomy}; ������ �����: {flight.AvailableSeatsBusiness}");
        }
    }
});

// �������� ������ ���������
async Task SendTicketToPassengerModule(Ticket ticket, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        var ticketJson = JsonSerializer.Serialize(ticket);
        var content = new StringContent(ticketJson, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(passengerModuleUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("����� ������� ��������� �� ������ ���������.");
        }
        else
        {
            Console.WriteLine($"������ ��� �������� ������: {response.StatusCode}");
        }
    }
}

/// �������� ��� ������ ������ ����� �������� �������
app.MapGet("/ticket-office/available-flights", async context =>
{
    var simulationTime = GetSimulationTime();
    var availableFlights = Flights //������ ������ �� ������� ����� ������ ������
        .Where(f => f.DepartureTime > simulationTime.AddHours(3)) // �����, �� ������� ��� ����� ������ ������
        .Select(f => new { f.Id, f.DepartureTime, f.AvailableSeatsEconomy, f.AvailableSeatsBusiness });

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
        await context.Response.WriteAsync("Invalid flight data.");
        return;
    }

        var existingFlight = Flights.FirstOrDefault(f => f.Id == newFlight.Id);
        if (existingFlight == null)
        {
            Flights.Add(newFlight);
            Console.WriteLine($"�������� ����� ����: ID {newFlight.Id}, �����: {newFlight.DepartureTime}");
        }
        else
        {
            // ��������� ������������ ���� (���� �����)
            existingFlight.DepartureTime = newFlight.DepartureTime;
            existingFlight.IsRegistrationOpen = newFlight.IsRegistrationOpen;
            existingFlight.AvailableSeatsEconomy = newFlight.AvailableSeatsEconomy;
            existingFlight.AvailableSeatsBusiness = newFlight.AvailableSeatsBusiness;
            Console.WriteLine($"�������� ����: ID {newFlight.Id}");
        }

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync("����� ������� ���������.");
});


app.Run();

