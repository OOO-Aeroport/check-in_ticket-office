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

// Хранилище данных
List<RegisteredPassengersEntry> RegisteredPassengers = new List<RegisteredPassengersEntry>();
List<Passenger> Passengers = new List<Passenger>();
List<Flight> flights = new List<Flight>();
List<Ticket> tickets = new List<Ticket>();
List<BaggageInfo> baggage = new List<BaggageInfo>();
List<FoodOrder> foodOrders= new List<FoodOrder>();

//функция отсчета времени
void CountTime()
{

}

//добавить заказ еды
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

//Найти зарегистрированных пассажиров по рейсу
List<RegisteredPassengersEntry> GetRegisteredPassengersByFlight(int flightID)
{
    List<RegisteredPassengersEntry> res = new List<RegisteredPassengersEntry>();
    for (int i = 0; i < RegisteredPassengers.Count; i++)
    {
        if (RegisteredPassengers[i].FlightID == flightID) res.Add(RegisteredPassengers[i]);
    }
    return res;
}

// Поиск рейса в списке по ID
int FindFlightByID(int id, List<Flight> flights)
{
    for (int i = 0; i < flights.Count; i++)
    {
        if (flights[i].Id == id) return i;
    }
    return -1;
}

//Получение рейса из списка по ID
Flight GetFlightByID(int id, List<Flight> flights)
{
    for (int i = 0; i < flights.Count; i++)
    {
        if (flights[i].Id == id) return flights[i];
    }
    return null;
}

//Получение подходящего рейса из списка по пассажиру
Flight GetSuitableFlight(Passenger psg, List<Flight> flights)
{
    for (int i = 0; i < flights.Count; i++)
    {
        if (flights[i].IsSuitable(psg)) return flights[i];
    }
    return null;
}

///Регистрация пассажира
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
        await context.Response.WriteAsync("Регистрация на рейс закрыта или рейс не существует.");
        return;
    }

    // Создаем пассажира
    var passenger = new RegisteredPassengersEntry(passengerId, "name", ticket.FlightId, ticket.Class);
    RegisteredPassengers.Add(passenger);

    // Отправляем ID пассажира в модуль пассажира для обновления статуса
    string passengerModuleUrl = "http://passenger-module-url/passenger/registration";
    await SendPassengerRegistrationStatus(passengerId, passengerModuleUrl);

    // Добавляем багаж
    baggage.Add(new BaggageInfo(passengerId, 20.5f)); // Пример веса багажа

    AddFoodOrder(flight.Id, ticket.Food);

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync($"Пассажир {passengerId} успешно зарегистрирован.");
});

//отправка статуса зарегистрирован пассажиру
async Task SendPassengerRegistrationStatus(int passengerId, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        // Создаем объект с ID пассажира
        var registrationData = new { PassengerId = passengerId };
        var registrationJson = JsonSerializer.Serialize(registrationData);
        var content = new StringContent(registrationJson, Encoding.UTF8, "application/json");

        // Отправляем POST-запрос
        var response = await httpClient.PostAsync(passengerModuleUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Статус пассажира {passengerId} успешно обновлен на 'Зарегистрирован'.");
        }
        else
        {
            Console.WriteLine($"Ошибка при обновлении статуса пассажира: {response.StatusCode}");
        }
    }
}

/// Обновление статуса регистрации рейса
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

    if (!flight.IsRegistrationOpen) //если регистрация закончилась
    {
        await SendRegisteredPassengers(GetRegisteredPassengersByFlight(flight.Id), departureBoardUrl);
        
        await SendFoodOrderToCateringService(foodOrders, cateringServiceUrl);
    }

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync($"Рейс {flight.Id} обновлен: регистрация {(flight.IsRegistrationOpen ? "открыта" : "закрыта")}.");
});



// отправка списка зарегистрированных пассажиров в табло
async Task SendRegisteredPassengers(List<RegisteredPassengersEntry> passengers, string departureBoardUrl)
{
    using (var httpClient = new HttpClient())
    {
        // Сериализация списка пассажиров в JSON
        var passengersJson = JsonSerializer.Serialize(passengers);
        var content = new StringContent(passengersJson, Encoding.UTF8, "application/json");

        // Отправка POST-запроса
        var response = await httpClient.PostAsync(departureBoardUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Список зарегистрированных пассажиров успешно отправлен на табло вылета.");
        }
        else
        {
            Console.WriteLine($"Ошибка при отправке списка пассажиров: {response.StatusCode}");
        }
    }
}


// Отправка информации о багаже в службу багажа
async Task SendBaggageInfo(List<BaggageInfo> luggageList, string luggageinfourl)
{
    using (var httpClient = new HttpClient())
    {
        // Сериализация списка заказов питания в JSON
        var foodOrdersJson = JsonSerializer.Serialize(foodOrders);
        var content = new StringContent(foodOrdersJson, Encoding.UTF8, "application/json");

        // Отправка POST-запроса
        var response = await httpClient.PostAsync(luggageinfourl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Информация о багаже успешно отправлена в службу багажа.");
        }
        else
        {
            Console.WriteLine($"Ошибка при отправке информации о багаже: {response.StatusCode}");
        }
    }
}

// Отправка заказов еды в слюжбу еды
async Task SendFoodOrderToCateringService(List<FoodOrder> foodOrders, string cateringServiceUrl)
{
    using (var httpClient = new HttpClient())
    {
        // Сериализация списка заказов питания в JSON
        var foodOrdersJson = JsonSerializer.Serialize(foodOrders);
        var content = new StringContent(foodOrdersJson, Encoding.UTF8, "application/json");

        // Отправка POST-запроса
        var response = await httpClient.PostAsync(cateringServiceUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Информация о питании успешно отправлена в службу питания.");
        }
        else
        {
            Console.WriteLine($"Ошибка при отправке информации о питании: {response.StatusCode}");
        }
    }
}

// Пассажир покупает билет
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
        await context.Response.WriteAsync("Невозможно купить билет.");
        return;
    }

    var ticket = new Ticket(psg.Id, flight.Id, flight.Gate, psg.ClassPreference, psg.FoodPreference);
    tickets[psg.Id] = ticket;
    flight.ReduceSeatsByClass(psg.ClassPreference);

    // Отправка билета на модуль пассажира
    string passengerModuleUrl = "http://other-module-url/passenger/ticket";
    await SendTicketToPassengerModule(ticket, passengerModuleUrl);

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync($"Билет для пассажира {psg.Id} успешно продан. Осталось мест: \n Эконом класс: {flight.AvailableSeatsEconomy}; Бизнес класс: {flight.AvailableSeatsBusiness}");
});

// Отправка билета пассажиру
async Task SendTicketToPassengerModule(Ticket ticket, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        var ticketJson = JsonSerializer.Serialize(ticket);
        var content = new StringContent(ticketJson, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(passengerModuleUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Билет успешно отправлен на модуль пассажира.");
        }
        else
        {
            Console.WriteLine($"Ошибка при отправке билета: {response.StatusCode}");
        }
    }
}

app.Run();
