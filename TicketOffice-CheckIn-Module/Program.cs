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
List<Passenger> Buyers = new List<Passenger>();
List<Flight> Flights = new List<Flight>();
List<BaggageInfo> Baggage = new List<BaggageInfo>();
List<FoodOrder> foodOrders= new List<FoodOrder>();

// Время симуляции
DateTime simulationStartTime = DateTime.MinValue; // Точка отсчета времени симуляции
bool isSimulationTimeSet = false;

// Запрос точки отсчета времени у табло при старте модуля
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
                Console.WriteLine($"Точка отсчета времени симуляции установлена: {simulationStartTime}");
            }
            else
            {
                Console.WriteLine($"Ошибка при запросе времени у табло: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при запросе времени у табло: {ex.Message}");
        }
    }
}

// Инициализация времени симуляции при старте модуля
await InitializeSimulationTime();

// Получение текущего времени симуляции
DateTime GetSimulationTime()
{
    if (!isSimulationTimeSet)
    {
        throw new InvalidOperationException("Simulation time has not been set.");
    }

    var realTimeElapsed = DateTime.Now - simulationStartTime;
    var simulationTimeElapsed = realTimeElapsed.TotalMinutes / 2; // 2 минуты реального времени = 1 час симуляции
    return simulationStartTime.AddHours(simulationTimeElapsed);
}

// Проверка завершения регистрации для всех рейсов
async Task CheckRegistrationCompletion()
{
    var simulationTime = GetSimulationTime();
    foreach (var flight in Flights)
    {
        if (flight.IsRegistrationOpen && simulationTime >= flight.RegistrationEndTime)
        {
            // Завершаем регистрацию
            flight.IsRegistrationOpen = false;
            Console.WriteLine($"Регистрация на рейс {flight.Id} завершена.");

            // Отправляем данные в табло, службу питания и службу багажа
            await SendRegistrationCompletionData(flight);
        }
    }
}

// Отправка данных о завершении регистрации
async Task SendRegistrationCompletionData(Flight flight)
{
    // Получаем список зарегистрированных пассажиров
    var registeredPassengers = RegisteredPassengers
        .Where(p => p.FlightID == flight.Id)
        .Select(p => new { p.PassengerID })
        .ToList();

    // Получаем список заказов еды
    var foodOrdersForFlight = foodOrders
        .Where(f => f.FlightID == flight.Id)
        .ToList();

    // Получаем список багажа
    var baggageForFlight = Baggage
        .Where(b => registeredPassengers.Any(p => p.PassengerID == b.PassengerID))
        .Select(b => new { b.PassengerID, b.BaggageWeight })
        .ToList();

    // Отправляем данные в табло
    string departureBoardUrl = "http://departure-board-url/registration-completion";
    await SendDataToService(departureBoardUrl, new { FlightID = flight.Id, Passengers = registeredPassengers });

    // Отправляем данные в службу питания
    string cateringServiceUrl = "http://catering-service-url/food-orders";
    await SendDataToService(cateringServiceUrl, new { FlightID = flight.Id, FoodOrders = foodOrdersForFlight });

    // Отправляем данные в службу багажа
    string luggageServiceUrl = "http://luggage-service-url/baggage-info";
    await SendDataToService(luggageServiceUrl, new { FlightID = flight.Id, Baggage = baggageForFlight });
}

// Общий метод для отправки данных
async Task SendDataToService(string url, object data)
{
    using (var httpClient = new HttpClient())
    {
        var jsonData = JsonSerializer.Serialize(data);
        var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(url, content);
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Данные успешно отправлены на {url}");
        }
        else
        {
            Console.WriteLine($"Ошибка при отправке данных на {url}: {response.StatusCode}");
        }
    }
}


//добавить заказ еды
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

Passenger GetPassengerByID(int id, List<Passenger> Passengers)
{
    for (int i = 0; i < Passengers.Count; i++)
    {
        if (Passengers[i].Id == id) return Passengers[i];
    }
    return null;
}

//Получение рейса из списка по ID
Flight GetFlightByID(int id, List<Flight> Flights)
{
    for (int i = 0; i < Flights.Count; i++)
    {
        if (Flights[i].Id == id) return Flights[i];
    }
    return null;
}

// Регистрация пассажира
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
        await context.Response.WriteAsync("Регистрация на рейс закрыта или рейс не существует.");
        return;
    }

    // Проверка времени регистрации
    var simulationTime = GetSimulationTime();
    if (simulationTime >= flight.RegistrationEndTime) // Регистрация заканчивается в указанное время
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Регистрация на рейс уже закрыта.");
        return;
    }

    // Создаем пассажира
    var passenger = new RegisteredPassengersEntry(passengerId, ticket.FlightID);
    RegisteredPassengers.Add(passenger);

    // Отправляем ID пассажира в модуль пассажира для обновления статуса
    string passengerModuleUrl = "http://passenger-module-url/passenger/registration";
    await SendPassengerRegistrationStatus(passengerId, passengerModuleUrl);

    // Добавляем багаж
    Baggage.Add(new BaggageInfo(passengerId, GetPassengerByID(passengerId, Buyers).BaggageWeight));

    AddFoodOrder(flight.Id, ticket.Food);

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync($"Пассажир {passengerId} успешно зарегистрирован.");
});

//отправка статуса зарегистрированному пассажиру
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

// Возврат билета
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
        await context.Response.WriteAsync("Билет не найден или является поддельным.");
        return;
    }

    var flight = GetFlightByID(request.Ticket.FlightID, Flights);
    if (flight == null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("Рейс не найден.");
        return;
    }

    var simulationTime = GetSimulationTime();
    if (simulationTime >= flight.DepartureTime.AddHours(-3)) // Возврат за 3 часа до вылета
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Билет нельзя вернуть: осталось менее 3 часов до вылета.");
        return;
    }

    // Возврат билета
    Buyers.Remove(byer);
    flight.ChangeSeatsByClass(request.Ticket.Class, 1);

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync($"Билет для пассажира {request.PassengerID} успешно возвращен.");
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

// Отправка заказов еды в службу еды
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

// Покупка билета
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
                await context.Response.WriteAsync("Невозможно купить билет на данный рейс.");
                return;
            }

            // Проверка времени покупки
            var simulationTime = GetSimulationTime();
            if (simulationTime >= flight.DepartureTime.AddHours(-3)) // Покупка заканчивается за 3 часа до вылета
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Покупка билетов на данный рейс закрыта.");
                return;
            }

            // Создаем билет
            var ticket = new Ticket(psg.Passenger.Id, flight.Id, flight.Gate, psg.Passenger.ClassPreference, psg.Passenger.FoodPreference);
            Buyers.Add(psg.Passenger);
            flight.ChangeSeatsByClass(ticket.Class, -1);

            // Отправка билета на модуль пассажира
            string passengerModuleUrl = "http://other-module-url/passenger/ticket";
            await SendTicketToPassengerModule(ticket, passengerModuleUrl);

            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync($"Билет для пассажира {psg.Passenger.Id} успешно продан. Осталось мест: \n Эконом класс: {flight.AvailableSeatsEconomy}; Бизнес класс: {flight.AvailableSeatsBusiness}");
        }
    }
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

/// Эндпоинт для выбора рейсов перед покупкой билетов
app.MapGet("/ticket-office/available-flights", async context =>
{
    var simulationTime = GetSimulationTime();
    var availableFlights = Flights //список рейсов на которые можно купить билеты
        .Where(f => f.DepartureTime > simulationTime.AddHours(3)) // Рейсы, на которые еще можно купить билеты
        .Select(f => new { f.Id, f.DepartureTime, f.AvailableSeatsEconomy, f.AvailableSeatsBusiness });

    await context.Response.WriteAsJsonAsync(availableFlights);
});

/// Дефолтный эндпоинт
app.MapGet("/", async context =>
{
    Console.WriteLine("Welcome to the Ticket Office / Check-In module!");
    await context.Response.WriteAsync("Welcome to the Ticket Office / Check-In module!");
});

// Эндпоинт для получения новых рейсов от табло
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
            Console.WriteLine($"Добавлен новый рейс: ID {newFlight.Id}, Вылет: {newFlight.DepartureTime}");
        }
        else
        {
            // Обновляем существующий рейс (если нужно)
            existingFlight.DepartureTime = newFlight.DepartureTime;
            existingFlight.IsRegistrationOpen = newFlight.IsRegistrationOpen;
            existingFlight.AvailableSeatsEconomy = newFlight.AvailableSeatsEconomy;
            existingFlight.AvailableSeatsBusiness = newFlight.AvailableSeatsBusiness;
            Console.WriteLine($"Обновлен рейс: ID {newFlight.Id}");
        }

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync("Рейсы успешно обновлены.");
});


app.Run();

