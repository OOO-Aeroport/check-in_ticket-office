using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Globalization; // Для CultureInfo
using System.Threading.Tasks;
using TicketOffice_CheckIn_Module;


var builder = WebApplication.CreateBuilder(args);

//Настройка Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5555); // Слушаем все IP-адреса на порту 5555
});
var app = builder.Build();


//URL модулей
string DepartureBoardUrl = "26.228.200.110:5555";
string CateringServiceUrl = "";
string LuggageServiceUrl = "26.132.135.106:5555";
string PassengerModuleUrl = "26.49.89.37:5555";


// Хранилище данных
List<PassengerEntry> RegisteredPassengers = new List<PassengerEntry>();
List<int> BuyerIDs = new List<int>();
List<Flight> Flights = new List<Flight>();
List<BaggageInfo> Baggage = new List<BaggageInfo>();
List<FoodOrder> foodOrders = new List<FoodOrder>();



// Получение текущего времени симуляции
async Task<DateTime> GetSimulationTime()
{
    using (var httpClient = new HttpClient())
    {
        string dbu = $"http://{DepartureBoardUrl}/departure-board/time";
        try
        {
            var response = await httpClient.GetAsync(dbu);
            if (response.IsSuccessStatusCode)
            {
                string responseData = await response.Content.ReadAsStringAsync();

                // Логируем полученные данные
                //Console.WriteLine($"Response data: {responseData}");

                // Удаляем лишние символы (например, кавычки)
                responseData = responseData.Trim('"');

                // Проверяем, что данные не пустые
                if (string.IsNullOrEmpty(responseData))
                {
                    Console.WriteLine("Response data is empty or null.");
                    throw new Exception("Empty or null response data.");
                }

                // Парсим строку с использованием правильного формата
                if (DateTime.TryParseExact(responseData, "yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime res))
                {
                    return res;
                }
                else
                {
                    Console.WriteLine($"Failed to parse simulation time: {responseData}");
                    throw new Exception("Invalid simulation time format.");
                }
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

// Проверка завершения регистрации для всех рейсов 
async Task CheckRegistrationStatus()
{
    var simulationTime = await GetSimulationTime();
    foreach (var flight in Flights)
    {
        if (!flight.IsRegistrationOpen && simulationTime >= flight.departureTime.AddHours(-3))
        {
            // Открываем регистрацию
            flight.IsRegistrationOpen = true;
            Console.WriteLine($"Check-in for flight {flight.id} is open.");
        }
        if (flight.IsRegistrationOpen && simulationTime >= flight.departureTime.AddMinutes(-30))
        {
            // Завершаем регистрацию
            flight.IsRegistrationOpen = false;
            Console.WriteLine($"Check-in for flight {flight.id} closed.");

            // Отправляем данные в табло, службу питания и службу багажа
            await SendRegistrationCompletionData(flight);
        }
    }
}

// Отправка данных о завершении регистрации
async Task SendRegistrationCompletionData(Flight flight)
{
    // Получаем список зарегистрированных пазырей
    var registeredPassengers = GetRegisteredPassengersByFlight(flight.id);
    // Получаем список заказов еды
    var foodOrdersForFlight = GetFoodOrderByFlight(flight.id);
    // Получаем список багажа
    var baggageForFlight = GetBaggageByFlight(flight.id);

    // Отправляем данные в табло
    string departureBoardUrl = $"http://{DepartureBoardUrl}/departure-board/flights/{flight.id}/passengers";
    await SendDataToService(departureBoardUrl, new { registeredPassengers });
    Console.WriteLine($"Departure table data sent successfully.");

    // Формируем данные для службы питания в нужном формате
    var cateringData = new
    {
        list_key = foodOrdersForFlight.Select(order => new
        {
            flight_id = flight.id,
            quantity = order.quantity
        }).ToList()
    };

    // Отправляем данные в службу питания
    string cateringServiceUrl = $"http://{CateringServiceUrl}/food-orders";
    await SendDataToService(cateringServiceUrl, cateringData);
    Console.WriteLine($"Catering Service data sent successfully.");

    // Отправляем данные в службу багажа
    string luggageServiceUrl = $"http://{LuggageServiceUrl}/transportation-bagg";
    await SendDataToService(luggageServiceUrl, new { FlightID = flight.id, Baggage = baggageForFlight });
    Console.WriteLine($"Luggage Service data sent successfully.");
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
            Console.WriteLine($"Data sent successfully to {url}");
        }
        else
        {
            Console.WriteLine($"Error sending data to {url}: {response.StatusCode}");
        }
    }
}

//добавить заказ еды
void AddFoodOrder(int id)
{
    for (int i = 0; i < foodOrders.Count; i++)
    {
        if (id == foodOrders[i].flight_id)
        {
            foodOrders[i].quantity++;
        }
    }
}

//Найти зарегистрированных пассажиров по рейсу
List<object> GetRegisteredPassengersByFlight(int flightID)
{
    List<object> res = new List<object>();
    for (int i = 0; i < RegisteredPassengers.Count; i++)
    {
        if (RegisteredPassengers[i].flight_id == flightID) res.Add(new { RegisteredPassengers[i].passenger_id });
    }
    if (res.Count > 0) Console.WriteLine($"Found RegisteredPassengers for flight {flightID}");
    return res;
}

bool IfBuyer(int passid)
{
    for (int i = 0; i < BuyerIDs.Count; i++) { if (BuyerIDs[i] == passid) return true; }
    return false;
}

List<FoodOrder> GetFoodOrderByFlight(int flightID)
{
    // Фильтруем заказы по flightID
    var orders = foodOrders.Where(order => order.flight_id == flightID).ToList();

    if (orders.Any())
    {
        Console.WriteLine($"Found {orders.Count} food orders for flight {flightID}");
    }
    else
    {
        Console.WriteLine($"Couldn't find food orders for flight {flightID}");
    }

    return orders;
}

//Получение рейса из списка по ID
Flight GetFlightByID(int id, List<Flight> Flights)
{
    for (int i = 0; i < Flights.Count; i++)
    {
        if (Flights[i].id == id) return Flights[i];
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

// Покупка билета
app.MapPost("/ticket-office/buy-ticket", async context =>
{
    var request = await context.Request.ReadFromJsonAsync<List<BuyRequest>>();
    Console.WriteLine(request);
    PassengerResponse resp;
    if (request == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        Console.WriteLine("Invalid request data.");
        resp = new PassengerResponse(-1, "Unsuccessful");
        return;
    }
    else
    {
        foreach (var psg in request)
        {
            Console.WriteLine(psg);

            var flight = GetFlightByID(psg.flight_id, Flights);
            Console.WriteLine(flight);

            string passengerModuleUrl = $"http://{PassengerModuleUrl}/passenger/ticket";

            if (flight == null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                Console.WriteLine("Impossible to buy a ticket for this flight.");

                resp = new PassengerResponse(psg.flight_id, "Unsuccessful");
                await SendPurchaseStatus(resp, passengerModuleUrl);
                return;
            }

            // Проверка времени покупки
            var simulationTime = await GetSimulationTime();
            if (simulationTime >= flight.departureTime.AddHours(-3)) // Покупка заканчивается за 3 часа до вылета
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                Console.WriteLine("Ticket sale for this flight is closed.");
                resp = new PassengerResponse(psg.passenger_id, "Unsuccessful");
                await SendPurchaseStatus(resp, passengerModuleUrl);
                return;
            }
            if (!flight.IsSuitable(psg.baggage_weight))
            {
                Console.WriteLine("The flight is unsuitable.");
                resp = new PassengerResponse(psg.passenger_id, "Unsuccessful");
                await SendPurchaseStatus(resp, passengerModuleUrl);
                return;
            }

            // Создаем билет
            Baggage.Add(new BaggageInfo(psg.passenger_id, psg.baggage_weight, flight.id));
            flight.seatsAvailable--;
            resp = new PassengerResponse(psg.passenger_id, "Successful");

            // Отправка билета на модуль пассажира
            await SendPurchaseStatus(resp, passengerModuleUrl);

            context.Response.StatusCode = StatusCodes.Status200OK;
            Console.WriteLine($"Ticket for passenger {psg.passenger_id} sold successfully. Seats left: {flight.seatsAvailable}");
        }
    }
});

//Отправка статуса покупки
async Task SendPurchaseStatus(PassengerResponse r, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        // Создаем объект с ID пассажира
        var purchaseData = new { PassengerId = r.PassengerID, Status = r.Status };
        var purchaseJson = JsonSerializer.Serialize(purchaseData);
        var content = new StringContent(purchaseJson, Encoding.UTF8, "application/json");

        // Отправляем POST-запрос
        var response = await httpClient.PostAsync(passengerModuleUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Пассажир {r.PassengerID} успешно купил билет.");
        }
        else
        {
            Console.WriteLine($"Ошибка: {response.StatusCode}");
        }
    }
}

// Регистрация пассажира
app.MapPost("/check-in/passenger", async context =>
{
    PassengerResponse resp;
    var request = await context.Request.ReadFromJsonAsync<List<PassengerEntry>>();
    if (request == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        Console.WriteLine("Invalid passenger data.");
        resp = new PassengerResponse(-1, "Unsuccessful");
        return;
    }
    foreach (var psg in request)
    {
        var passengerId = psg.passenger_id;
        var flight = GetFlightByID(psg.flight_id, Flights);

        if (flight == null || !flight.IsRegistrationOpen)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            Console.WriteLine("Check-in for the flight is closed or the flight doesn't exist.");
            resp = new PassengerResponse(psg.passenger_id, "Unsuccessful");
            return;
        }

        // Проверка времени регистрации
        var simulationTime = await GetSimulationTime();
        if (simulationTime >= flight.departureTime.AddMinutes(-30)) // Регистрация заканчивается в указанное время
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            Console.WriteLine("Check-in for the flight is closed.");
            resp = new PassengerResponse(psg.passenger_id, "Unsuccessful");
            return;
        }

        // Регистрируем пассажира
        RegisteredPassengers.Add(new PassengerEntry(psg.passenger_id, psg.flight_id));
        AddFoodOrder(flight.id);

        // Отправляем ID пассажира в модуль пассажира для обновления статуса
        string passengerModuleUrl = $"http://{PassengerModuleUrl}/passenger/registration";
        resp = new PassengerResponse(psg.passenger_id, "Successful");
        await SendPassengerRegistrationStatus(resp, passengerModuleUrl);

        context.Response.StatusCode = StatusCodes.Status200OK;
        Console.WriteLine($"Passenger {passengerId} registered successfully.");
    }
});

//отправка статуса зарегистрированному пассажиру
async Task SendPassengerRegistrationStatus(PassengerResponse p, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        // Создаем объект с ID пассажира
        var registrationData = new { PassengerId = p.PassengerID, Status = p.Status };
        var registrationJson = JsonSerializer.Serialize(registrationData);
        var content = new StringContent(registrationJson, Encoding.UTF8, "application/json");

        // Отправляем POST-запрос
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


// Возврат билета
app.MapPost("/ticket-office/return-ticket", async context =>
{
    PassengerResponse resp;
    var request = await context.Request.ReadFromJsonAsync<List<PassengerEntry>>();
    if (request == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        Console.WriteLine("Invalid request data.");
        resp = new PassengerResponse(-1, "Unsuccessful");
        return;
    }
    foreach (var psg in request)
    {
        if (!IfBuyer(psg.passenger_id))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            Console.WriteLine("Ticket not found or forged.");
            resp = new PassengerResponse(psg.passenger_id, "Unsuccessful");
            return;
        }

        var flight = GetFlightByID(psg.flight_id, Flights);
        if (flight == null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            Console.WriteLine("Flight not found.");
            resp = new PassengerResponse(psg.passenger_id, "Unsuccessful");
            return;
        }

        var simulationTime = await GetSimulationTime();
        if (simulationTime >= flight.departureTime.AddHours(-3)) // Возврат за 3 часа до вылета
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            Console.WriteLine("Unable to return ticket: less than 3 hours before departure.");
            resp = new PassengerResponse(psg.passenger_id, "Unsuccessful");
            return;
        }

        // Возврат билета
        BuyerIDs.Remove(psg.passenger_id);
        flight.seatsAvailable++;
        string returnModuleUrl = $"http://{PassengerModuleUrl}/passenger/available-flights";
        resp = new PassengerResponse(psg.passenger_id, "Successful");
        await SendReturnStatus(resp, returnModuleUrl);
        context.Response.StatusCode = StatusCodes.Status200OK;
        Console.WriteLine($"Ticket for passenger {psg.passenger_id} returned successfully.");
    }
});

//отправка статуса пассажиру, возвратившему билет
async Task SendReturnStatus(PassengerResponse p, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        // Создаем объект с ID пассажира
        var registrationData = new { PassengerId = p.PassengerID, Status = p.Status };
        var registrationJson = JsonSerializer.Serialize(registrationData);
        var content = new StringContent(registrationJson, Encoding.UTF8, "application/json");

        // Отправляем POST-запрос
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


List<FlightInfo> GetAvailableFlights(DateTime curr)
{
    List<FlightInfo> res = new List<FlightInfo>();
    foreach (var flight in Flights)
    {
        if (curr <= flight.departureTime.AddHours(-3)) res.Add(new FlightInfo(flight.id, flight.departureTime.AddHours(-3).ToString(), flight.departureTime.ToString()));
    }
    return res;
}

///// Эндпоинт для выбора рейсов перед покупкой билетов
//app.MapGet("/ticket-office/available-flights", async context =>
//{
//    var simulationTime = await GetSimulationTime();
//    //List<FlightInfo> availableFlights = GetAvailableFlights(simulationTime); //список рейсов на которые можно купить билеты
//    List<FlightInfo> availableFlights = new List<FlightInfo>();
//    availableFlights.Add(new FlightInfo(111,DateTime.Now.AddHours(-3).ToString(),DateTime.Now.ToString()));
//    availableFlights.Add(new FlightInfo(222, DateTime.Now.AddHours(-3).ToString(), DateTime.Now.ToString()));
//    Console.WriteLine("done");
//    string dep = $"http://{PassengerModuleUrl}/passenger/available-flights";

//    await context.Response.WriteAsJsonAsync(availableFlights);
//});

/// Эндпоинт для выбора рейсов перед покупкой билетов
app.MapGet("/ticket-office/available-flights", async context =>
{
    var simulationTime = await GetSimulationTime();
    List<FlightInfo> availableFlights = GetAvailableFlights(simulationTime); //список рейсов на которые можно купить билеты
    // Создаем список доступных рейсов
    //List<FlightInfo> availableFlights = new List<FlightInfo>
    //{
    //    new FlightInfo(111, simulationTime.AddHours(-3).ToString(), DateTime.Now.ToString()),
    //    new FlightInfo(222, DateTime.Now.AddHours(-3).ToString(), DateTime.Now.ToString())
    //};

    Console.WriteLine("Available flights retrieved.");

    // Отправляем данные на указанный эндпоинт
    string passengerModuleUrl = $"http://{PassengerModuleUrl}/passenger/available-flights";
    await SendAvailableFlights(availableFlights, passengerModuleUrl);
    Console.WriteLine("yay");
    // Возвращаем список рейсов в ответе
    await context.Response.WriteAsJsonAsync(availableFlights);
});

/// Метод для отправки списка доступных рейсов
async Task SendAvailableFlights(List<FlightInfo> flights, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {

        Flights.Add(new Flight(111, (await GetSimulationTime()).AddHours(4), false, 100, 100));
        Flights.Add(new Flight(222, (await GetSimulationTime()).AddHours(5), false, 100, 100));
        Flights.Add(new Flight(333, (await GetSimulationTime()).AddHours(4), false, 100, 100));
        Flights.Add(new Flight(444, (await GetSimulationTime()).AddHours(4), false, 100, 100));

        // Сериализуем список рейсов в JSON
        var jsonData = JsonSerializer.Serialize(flights);
        var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

        // Отправляем POST-запрос
        var response = await httpClient.PostAsync(passengerModuleUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Available flights sent successfully.");
        }
        else
        {
            Console.WriteLine($"Error sending available flights: {response.StatusCode}");
        }
    }
}

/// Дефолтный эндпоинт
app.MapGet("/", async context =>
{
    Console.WriteLine("Welcome to the Ticket Office / Check-In module!");
    await context.Response.WriteAsync("Welcome to the Ticket Office / Check-In module!");
    //PassengerResponse resp;
    //List<object> passengers = new List<object>();
    //List<BaggageInfo> bg = new List<BaggageInfo>();
    //List<FoodOrder> fo = new List<FoodOrder>();
    //passengers.Add(new { passengerId = 1112 });
    //passengers.Add(new { passengerId = 7786 });
    //bg.Add(new BaggageInfo(111, 3,222));
    //bg.Add(new BaggageInfo(444, 5, 333));
    //fo.Add(new FoodOrder(111));
    //fo.Add(new FoodOrder(333));
    // Формируем данные для службы питания в нужном формате
    //var cateringData = new
    //{
    //    list_key = fo.Select(order => new
    //    {
    //        flight_id = 111,
    //        quantity = order.quantity
    //    }).ToList()
    //};
    //await SendDataToService($"http://{DepartureBoardUrl}/departure-board/flights/{50}/passengers", passengers);
    //await context.Response.WriteAsJsonAsync(rp);

});

// Эндпоинт для получения новых рейсов от табло
app.MapPost("ticket-office/flights", async context =>
{
    var newFlight = await context.Request.ReadFromJsonAsync<Flight>();
    if (newFlight == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        Console.WriteLine("Invalid flight data.");
        return;
    }

    var existingFlight = Flights.FirstOrDefault(f => f.id == newFlight.id);
    if (existingFlight == null)
    {
        Flights.Add(newFlight);
        Console.WriteLine($"New flight added: ID {newFlight.id}, Departure: {newFlight.departureTime}");
    }
    else
    {
        // Обновляем существующий рейс (если нужно)
        existingFlight.departureTime = newFlight.departureTime;
        existingFlight.IsRegistrationOpen = newFlight.IsRegistrationOpen;
        existingFlight.seatsAvailable = newFlight.seatsAvailable;
        Console.WriteLine($"Flight updated: ID {newFlight.id}");
    }

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync("Flights updated successfully.");
});

var timer = new System.Timers.Timer(2000); // Проверка каждую минуту симуляции
timer.Elapsed += async (sender, e) =>
{
    try
    {
        // Получаем текущее время симуляции
        //var simulationTime = await GetSimulationTime();

        // Выводим время симуляции в консоль
        //Console.WriteLine($"Current simulation time: {simulationTime}");

        // Проверяем статус регистрации
        await CheckRegistrationStatus();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in timer event: {ex.Message}");
    }
}; timer.Start();

app.Run();