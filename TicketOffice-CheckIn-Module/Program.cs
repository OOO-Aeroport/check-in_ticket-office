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
List<Passenger> Passengers = new List<Passenger>();
List<Flight> Flights = new List<Flight>();
List<Ticket> SoldTickets = new List<Ticket>();
List<BaggageInfo> baggage = new List<BaggageInfo>();
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

//�������� ����� ���
void AddFoodOrder(int id, string food)
{
    for (int i = 0; i < foodOrders.Count; i++)
    {
        if (id == foodOrders[i].FlightID)
        {
            if (food == foodOrders[i].food.FoodType) foodOrders[i].food.Quantity++;
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

// ����� ����� � ������ �� ID
int FindFlightByID(int id, List<Flight> Flights)
{
    for (int i = 0; i < Flights.Count; i++)
    {
        if (Flights[i].Id == id) return i;
    }
    return -1;
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
    var request = await context.Request.ReadFromJsonAsync<PassengerRegistrationRequest>();
    if (request == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Invalid passenger data.");
        return;
    }

    var passengerId = request.PassengerId;
    var ticket = request.Ticket;
    var flight = GetFlightByID(ticket.FlightId, Flights);

    if (ticket == null || flight == null || !flight.IsRegistrationOpen)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("����������� �� ���� ������� ��� ���� �� ����������.");
        return;
    }

    // �������� ������� �����������
    var simulationTime = GetSimulationTime();
    if (simulationTime >= flight.DepartureTime.AddMinutes(-30)) // ����������� ������������� �� 30 ����� �� ������
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("����������� �� ���� ��� �������.");
        return;
    }

    // ������� ���������
    var passenger = new RegisteredPassengersEntry(passengerId, "name", ticket.FlightId, ticket.Class);
    RegisteredPassengers.Add(passenger);

    // ���������� ID ��������� � ������ ��������� ��� ���������� �������
    string passengerModuleUrl = "http://passenger-module-url/passenger/registration";
    await SendPassengerRegistrationStatus(passengerId, passengerModuleUrl);

    // ��������� �����
    baggage.Add(new BaggageInfo(passengerId, 20.5f)); // ������ ���� ������

    AddFoodOrder(flight.Id, ticket.Food);

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync($"�������� {passengerId} ������� ���������������.");
});

//�������� ������� ��������������� ���������
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
    var request = await context.Request.ReadFromJsonAsync<ReturnTicketRequest>();
    if (request == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Invalid request data.");
        return;
    }

    var ticket = SoldTickets.FirstOrDefault(t => t.PassengerId == request.PassengerID && t.FlightId == request.FlightID);
    if (ticket == null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("����� �� ������ ��� �������� ����������.");
        return;
    }

    var flight = GetFlightByID(ticket.FlightId, Flights);
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
    SoldTickets.Remove(ticket);
    flight.ChangeSeatsByClass(ticket.Class, 1);

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync($"����� ��� ��������� {request.PassengerID} ������� ���������.");
});

/// ���������� ������� ����������� �����
app.MapPost("/check-in/flights", async context =>
{
    var flight = await context.Request.ReadFromJsonAsync<Flight>();
    if (flight == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Invalid flight data.");
        return;
    }
    string departureBoardUrl = "http://departure-board-url/departure-board/passengers";
    string cateringServiceUrl = "http://catering-service-url/food-orders";
    string luggageServiceUrl = "http://luggage-service-url/transport-passengers";

    int id = FindFlightByID(flight.Id, Flights);
    Flights[id].IsRegistrationOpen = flight.IsRegistrationOpen;

    if (!flight.IsRegistrationOpen) //���� ����������� �����������
    {
        await SendRegisteredPassengers(GetRegisteredPassengersByFlight(flight.Id), departureBoardUrl);
        
        await SendFoodOrderToCateringService(foodOrders, cateringServiceUrl);
    }

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync($"���� {flight.Id} ��������: ����������� {(flight.IsRegistrationOpen ? "�������" : "�������")}.");
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
    var request = await context.Request.ReadFromJsonAsync<BuyTicketRequest>();
    if (request == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Invalid request data.");
        return;
    }

    var flight = GetFlightByID(request.FlightID, Flights);
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
    var ticket = new Ticket(request.PassengerID, flight.Id, flight.Gate, request.Class, request.Food);
    SoldTickets.Add(ticket);
    flight.ChangeSeatsByClass(request.Class, -1);

    // �������� ������ �� ������ ���������
    string passengerModuleUrl = "http://other-module-url/passenger/ticket";
    await SendTicketToPassengerModule(ticket, passengerModuleUrl);

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync($"����� ��� ��������� {request.PassengerID} ������� ������. �������� ����: \n ������ �����: {flight.AvailableSeatsEconomy}; ������ �����: {flight.AvailableSeatsBusiness}");
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
    var availableFlights = Flights
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
app.MapPost("/flights", async context =>
{
    var newFlights = await context.Request.ReadFromJsonAsync<List<Flight>>();
    if (newFlights == null || newFlights.Count == 0)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Invalid flight data.");
        return;
    }

    // ��������� ����� ����� � ������
    foreach (var flight in newFlights)
    {
        var existingFlight = Flights.FirstOrDefault(f => f.Id == flight.Id);
        if (existingFlight == null)
        {
            Flights.Add(flight);
            Console.WriteLine($"�������� ����� ����: ID {flight.Id}, �����: {flight.DepartureTime}");
        }
        else
        {
            // ��������� ������������ ���� (���� �����)
            existingFlight.DepartureTime = flight.DepartureTime;
            existingFlight.IsRegistrationOpen = flight.IsRegistrationOpen;
            existingFlight.AvailableSeatsEconomy = flight.AvailableSeatsEconomy;
            existingFlight.AvailableSeatsBusiness = flight.AvailableSeatsBusiness;
            Console.WriteLine($"�������� ����: ID {flight.Id}");
        }
    }

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync("����� ������� ���������.");
});


app.Run();

