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
List<Flight> flights = new List<Flight>();
List<Ticket> tickets = new List<Ticket>();
List<BaggageInfo> baggage = new List<BaggageInfo>();
List<FoodOrder> foodOrders= new List<FoodOrder>();

//������� ������� �������
void CountTime()
{

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
int FindFlightByID(int id, List<Flight> flights)
{
    for (int i = 0; i < flights.Count; i++)
    {
        if (flights[i].Id == id) return i;
    }
    return -1;
}

//��������� ����� �� ������ �� ID
Flight GetFlightByID(int id, List<Flight> flights)
{
    for (int i = 0; i < flights.Count; i++)
    {
        if (flights[i].Id == id) return flights[i];
    }
    return null;
}

//��������� ����������� ����� �� ������ �� ���������
Flight GetSuitableFlight(Passenger psg, List<Flight> flights)
{
    for (int i = 0; i < flights.Count; i++)
    {
        if (flights[i].IsSuitable(psg)) return flights[i];
    }
    return null;
}

///����������� ���������
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
    var flight = GetFlightByID(ticket.FlightId, flights);

    if (ticket == null || flight == null || !flight.IsRegistrationOpen)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("����������� �� ���� ������� ��� ���� �� ����������.");
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
    string luggageServiceUrl = "http://catering-service-url/transport-passengers";

    int id = FindFlightByID(flight.Id, flights);
    flights[id].IsRegistrationOpen = flight.IsRegistrationOpen;

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

// �������� �������� �����
app.MapPost("/ticket-office/passenger", async context =>
{
    Passenger psg = await context.Request.ReadFromJsonAsync<Passenger>();
    if (psg == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Invalid passenger data.");
        return;
    }
    Flight flight = GetSuitableFlight(psg, flights);
    if (flight == null || !flight.IsRegistrationOpen)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("���������� ������ �����.");
        return;
    }

    var ticket = new Ticket(psg.Id, flight.Id, flight.Gate, psg.ClassPreference, psg.FoodPreference);
    tickets[psg.Id] = ticket;
    flight.ReduceSeatsByClass(psg.ClassPreference);

    // �������� ������ �� ������ ���������
    string passengerModuleUrl = "http://other-module-url/passenger/ticket";
    await SendTicketToPassengerModule(ticket, passengerModuleUrl);

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync($"����� ��� ��������� {psg.Id} ������� ������. �������� ����: \n ������ �����: {flight.AvailableSeatsEconomy}; ������ �����: {flight.AvailableSeatsBusiness}");
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

app.Run();
