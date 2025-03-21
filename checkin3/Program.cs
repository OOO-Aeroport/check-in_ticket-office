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

// Добавление middleware для обслуживания статических файлов
app.UseStaticFiles(); // Разрешает доступ к файлам в папке wwwroot

//URL модулей
string DepartureBoardUrl = "26.228.200.110:5555";
string UnoUrl = "26.53.143.176:5555";
string PassengerModuleUrl = "26.49.89.37:5555";
string PlaneUrl = "26.125.155.211:5555";


// Хранилище данных
List<PassengerEntry> RegisteredPassengers = new List<PassengerEntry>();
List<int> BuyerIDs = new List<int>();
List<Flight> Flights = new List<Flight>();
List<BaggageInfo> Baggage = new List<BaggageInfo>();


#region
// Получение текущего времени симуляции
//async Task<DateTime> GetSimulationTime()
//{
//    using (var httpClient = new HttpClient())
//    {
//        string dbu = $"http://{DepartureBoardUrl}/departure-board/time";
//        try
//        {
//            var response = await httpClient.GetAsync(dbu);
//            if (response.IsSuccessStatusCode)
//            {
//                string responseData = await response.Content.ReadAsStringAsync();

//                // Логируем полученные данные
//                //Console.WriteLine($"Response data: {responseData}");

//                // Удаляем лишние символы (например, кавычки)
//                responseData = responseData.Trim('"');

//                // Проверяем, что данные не пустые
//                if (string.IsNullOrEmpty(responseData))
//                {
//                    Console.WriteLine("Response data is empty or null.");
//                    throw new Exception("Empty or null response data.");
//                }

//                // Парсим строку с использованием правильного формата
//                if (DateTime.TryParseExact(responseData, "yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime res))
//                {
//                    return res;
//                }
//                else
//                {
//                    Console.WriteLine($"Failed to parse simulation time: {responseData}");
//                    throw new Exception("Invalid simulation time format.");
//                }
//            }
//            else
//            {
//                Console.WriteLine($"Departure board request error: {response.StatusCode}");
//                throw new Exception("Unable to get simulation time.");
//            }
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine($"Departure board request error: {ex.Message}");
//            throw;
//        }
//    }
//}

// Проверка завершения регистрации для всех рейсов 
//async Task CheckRegistrationStatus()
//{
//    var simulationTime = await GetSimulationTime();
//    foreach (var flight in Flights)
//    {
//        if (!flight.IsRegistrationOpen && simulationTime >= flight.departureTime.AddHours(-3))
//        {
//            // Открываем регистрацию
//            flight.IsRegistrationOpen = true;
//            Console.WriteLine($"Check-in for flight {flight.id} is open.");
//        }
//        if (flight.IsRegistrationOpen && simulationTime >= flight.departureTime.AddMinutes(-30))
//        {
//            // Завершаем регистрацию
//            flight.IsRegistrationOpen = false;
//            Console.WriteLine($"Check-in for flight {flight.id} closed.");

//            // Отправляем данные в табло, службу питания и службу багажа
//            await SendRegistrationCompletionData(flight);
//        }
//    }
//}
#endregion

// Отправка данных о завершении регистрации
async Task SendRegistrationCompletionData(Flight flight)
{
    // Получаем список зарегистрированных 
    var registeredPassengers = GetRegisteredPassengersByFlight(flight.FlightId);
    Console.WriteLine($"registeredPassengers: {registeredPassengers.ToString()}");
    // Получаем список заказов еды
    var foodOrderForFlight = registeredPassengers.Count;
    Console.WriteLine(foodOrderForFlight.ToString());
    // Получаем список багажа
    int baggageForFlight = GetBaggageByFlight(flight.FlightId);

    // Формируем данные для uno в нужном формате
    var unoData = new
    {
        planeId = flight.AirplaneID,
        passengers = registeredPassengers,
        food = foodOrderForFlight,
        baggage = baggageForFlight
    };

    // Отправляем данные в Уно
    string UNOURL = $"http://{UnoUrl}/uno/api/v1/order/order-from-registration";
    //await SendDataToService($"http://localhost:5555/a", unoData);
    await SendDataToService(UNOURL, unoData);
    Console.WriteLine($"Plane data sent successfully.");

    string PURL = $"http://{PlaneUrl}/reg_passengers/{flight.AirplaneID}";
    //await SendDataToService($"http://localhost:5555/a", unoData);
    await SendDataToService(PURL, registeredPassengers);
    Console.WriteLine($"Plane data sent successfully.");
}

// Общий метод для отправки данных
async Task SendDataToService(string url, object data)
{
    using (var httpClient = new HttpClient())
    {

        var options = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var response = await httpClient.PostAsJsonAsync(url, data, options);
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
void AddBaggage(int flightId, int quantity)
{
    for (int i = 0; i < Baggage.Count; i++)
    {
        if (flightId == Baggage[i].FlightID)
        {
            Baggage[i].Quantity += quantity;
        }
    }
}

//Найти зарегистрированных пассажиров по рейсу
List<object> GetRegisteredPassengersByFlight(int flightID)
{
    List<object> res = new List<object>();
    for (int i = 0; i < RegisteredPassengers.Count; i++)
    {
        if (RegisteredPassengers[i].flight_id == flightID)
        {
            res.Add(new { passengerId = RegisteredPassengers[i].passenger_id });
        }
    }

    if (res.Count > 0)
    {
        Console.WriteLine($"Found RegisteredPassengers for flight {flightID}");
    }
    else
    {
        Console.WriteLine($"No registered passengers found for flight {flightID}");
    }

    return res;
}

bool IfBuyer(int passid)
{
    for (int i = 0; i < BuyerIDs.Count; i++) { if (BuyerIDs[i] == passid) return true; }
    return false;
}


//Получение рейса из списка по ID
Flight GetFlightByID(int id, List<Flight> Flights)
{
    for (int i = 0; i < Flights.Count; i++)
    {
        if (Flights[i].FlightId == id) return Flights[i];
    }
    return null;
}

int GetBaggageByFlight(int flightID)
{
    int res = 0;
    foreach (var bg in Baggage)
    {
        if (bg.FlightID == flightID)
        {
            res = bg.Quantity;
            break;
        }
    }
    return res;
}

app.MapPost("/ticket-office/buy-ticket", async context =>
{
    var request = await context.Request.ReadFromJsonAsync<List<BuyRequest>>();
    Console.WriteLine(request);
    List<PassengerResponse> lpr = new List<PassengerResponse>();

    if (request == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        Console.WriteLine("Invalid request data.");
        await context.Response.WriteAsJsonAsync(new PassengerResponse(-1, "Unsuccessful"));
        return;
    }

    foreach (var psg in request)
    {
        Console.WriteLine(psg.baggageQuantity);

        var flight = GetFlightByID(psg.flightId, Flights);
        if (flight != null ) Console.WriteLine(flight.FlightId);

        string passengerModuleUrl = $"http://{PassengerModuleUrl}/passenger/ticket";

        if (flight == null)
        {
            Console.WriteLine("Impossible to buy a ticket for this flight.");
            lpr.Add(new PassengerResponse(psg.passengerId, "Unsuccessful"));
            continue;
        }

        // Проверка времени покупки
        //var simulationTime = await GetSimulationTime();
        if (flight.RegistrationState != 0) // Покупка заканчивается за 3 часа до вылета
        {
            Console.WriteLine("Ticket sale for this flight is closed.");
            lpr.Add(new PassengerResponse(psg.passengerId, "Unsuccessful"));
            continue;
        }

        if (!flight.IsSuitable(psg.baggageQuantity))
        {
            Console.WriteLine("The flight is unsuitable.");
            lpr.Add(new PassengerResponse(psg.passengerId, "Unsuccessful"));
            continue;
        }

        // Создаем билет
        AddBaggage(flight.FlightId, psg.baggageQuantity);
        BuyerIDs.Add(psg.passengerId);
        flight.seatsAvailable--;
        flight.baggageAvailable -= psg.baggageQuantity;
        lpr.Add(new PassengerResponse(psg.passengerId, "Successful"));

        Console.WriteLine($"Ticket for passenger {psg.passengerId} sold successfully. Seats left: {flight.seatsAvailable}");
    }

    // Отправка статуса покупки для всех пассажиров
    await SendPurchaseStatus(lpr, $"http://{PassengerModuleUrl}/passenger/ticket");

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsJsonAsync(lpr);
});

// Отправка статуса покупки для списка пассажиров
async Task SendPurchaseStatus(List<PassengerResponse> responses, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        // Создаем объект с ID пассажиров и их статусами
        var purchaseData = responses.Select(r => new { PassengerId = r.PassengerID, Status = r.Status }).ToList();
        var purchaseJson = JsonSerializer.Serialize(purchaseData);
        var content = new StringContent(purchaseJson, Encoding.UTF8, "application/json");

        // Отправляем POST-запрос
        var response = await httpClient.PostAsync(passengerModuleUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Статус покупки успешно отправлен для всех пассажиров.");
        }
        else
        {
            Console.WriteLine($"Ошибка: {response.StatusCode}");
        }
    }
}

// Регистрация пассажира
app.MapPost("/checkin/passenger", async context =>
{
    var request = await context.Request.ReadFromJsonAsync<List<PassengerEntry>>();
    List<PassengerResponse> responses = new List<PassengerResponse>();

    if (request == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        Console.WriteLine("Invalid passenger data.");
        await context.Response.WriteAsJsonAsync(new PassengerResponse(-1, "Unsuccessful"));
        return;
    }

    foreach (var psg in request)
    {
        var passengerId = psg.passenger_id;
        var flight = GetFlightByID(psg.flight_id, Flights);

        if (flight == null || flight.RegistrationState !=1)
        {
            Console.WriteLine("Check-in for the flight is closed or the flight doesn't exist.");
            responses.Add(new PassengerResponse(psg.passenger_id, "Unsuccessful"));
            continue;
        }

        // Проверка времени регистрации
        //var simulationTime = await GetSimulationTime();
        if (flight.RegistrationState !=1) // Регистрация заканчивается в указанное время
        {
            Console.WriteLine("Check-in for the flight is closed.");
            responses.Add(new PassengerResponse(psg.passenger_id, "Unsuccessful"));
            continue;
        }
        if (!IfBuyer(passengerId))
        {
            Console.WriteLine("Ticket forged.");
            responses.Add(new PassengerResponse(psg.passenger_id, "Unsuccessful"));
            continue;
        }
        // Регистрируем пассажира
        RegisteredPassengers.Add(new PassengerEntry(psg.passenger_id, psg.flight_id));
 

        // Добавляем успешный ответ
        responses.Add(new PassengerResponse(psg.passenger_id, "Successful"));

        Console.WriteLine($"Passenger {passengerId} registered successfully.");
    }

    // Отправка статуса регистрации для всех пассажиров
    await SendPassengerRegistrationStatus(responses, $"http://{PassengerModuleUrl}/passenger/check-in");

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsJsonAsync(responses);
});

// Отправка статуса регистрации для списка пассажиров
async Task SendPassengerRegistrationStatus(List<PassengerResponse> responses, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        // Создаем объект с ID пассажиров и их статусами
        var registrationData = responses.Select(r => new { PassengerId = r.PassengerID, Status = r.Status }).ToList();
        var registrationJson = JsonSerializer.Serialize(registrationData);
        var content = new StringContent(registrationJson, Encoding.UTF8, "application/json");

        // Отправляем POST-запрос
        var response = await httpClient.PostAsync(passengerModuleUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Check-in status sent successfully to all passengers.");
        }
        else
        {
            Console.WriteLine($"Error: {response.StatusCode}");
        }
    }
}


// Возврат билета
app.MapPost("/ticket-office/return-ticket", async context =>
{
    var request = await context.Request.ReadFromJsonAsync<List<BuyRequest>>();
    List<PassengerResponse> responses = new List<PassengerResponse>();

    if (request == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        Console.WriteLine("Invalid request data.");
        await context.Response.WriteAsJsonAsync(new PassengerResponse(-1, "Unsuccessful"));
        return;
    }

    foreach (var psg in request)
    {
        PassengerResponse resp;

        if (!IfBuyer(psg.passengerId))
        {
            Console.WriteLine("Ticket not found or forged.");
            responses.Add(new PassengerResponse(psg.passengerId, "Unsuccessful"));
            continue;
        }

        var flight = GetFlightByID(psg.flightId, Flights);
        if (flight == null)
        {
            Console.WriteLine("Flight not found.");
            responses.Add(new PassengerResponse(psg.passengerId, "Unsuccessful"));
            continue;
        }

        //var simulationTime = await GetSimulationTime();
        if (flight.RegistrationState != 0) // Возврат за 3 часа до вылета
        {
            Console.WriteLine("Unable to return ticket: less than 3 hours before departure.");
            responses.Add(new PassengerResponse(psg.passengerId, "Unsuccessful"));
            continue;
        }

        // Возврат билета
        BuyerIDs.Remove(psg.passengerId);
        flight.baggageAvailable += psg.baggageQuantity;
        flight.seatsAvailable++;
        AddBaggage(psg.flightId, -psg.baggageQuantity);
        resp = new PassengerResponse(psg.passengerId, "Successful");
        responses.Add(resp);

        Console.WriteLine($"Ticket for passenger {psg.passengerId} returned successfully.");
    }

    // Отправка статуса возврата для всех пассажиров
    await SendReturnStatus(responses, $"http://{PassengerModuleUrl}/passenger/return-ticket");

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsJsonAsync(responses);
});

// Отправка статуса возврата для списка пассажиров
async Task SendReturnStatus(List<PassengerResponse> responses, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        // Создаем объект с ID пассажиров и их статусами
        var returnData = responses.Select(r => new { PassengerId = r.PassengerID, Status = r.Status }).ToList();
        var returnJson = JsonSerializer.Serialize(returnData);
        var content = new StringContent(returnJson, Encoding.UTF8, "application/json");

        // Отправляем POST-запрос
        var response = await httpClient.PostAsync(passengerModuleUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Return status sent successfully to all passengers.");
        }
        else
        {
            Console.WriteLine($"Ошибка: {response.StatusCode}");
        }
    }
}

List<FlightInfo> GetAvailableFlights()
{
    List<FlightInfo> res = new List<FlightInfo>();
    foreach (var flight in Flights)
    {
        if (flight.RegistrationState == 0) res.Add(new FlightInfo(flight.FlightId, (flight.departureTime.AddHours(-3)).ToString(), flight.departureTime.ToString()));
    }
    return res;
}

#region
/// Эндпоинт для выбора рейсов перед покупкой билетов
//app.MapGet("/ticket-office/available-flights", async context =>
//{
//    //var simulationTime = await GetSimulationTime();
//    Flights.Add(new Flight(11,1, DateTime.Now,100,100));
//    Flights.Add(new Flight(22,2, DateTime.Now, 100, 100));
//    List<FlightInfo> availableFlights = GetAvailableFlights(); //список рейсов на которые можно купить билеты

//    Console.WriteLine("Available flights retrieved.");

//    // Отправляем данные на указанный эндпоинт
//    string passengerModuleUrl = $"http://{PassengerModuleUrl}/passenger/available-flights";
//    await SendAvailableFlights(availableFlights, passengerModuleUrl);

//    // Возвращаем список рейсов в ответе
//    await context.Response.WriteAsJsonAsync(availableFlights);
//});

/// Метод для отправки списка доступных рейсов
//async Task SendAvailableFlights(List<FlightInfo> flights, string passengerModuleUrl)
//{
//    using (var httpClient = new HttpClient())
//    {
//        // Сериализуем список рейсов в JSON
//        var jsonData = JsonSerializer.Serialize(flights);
//        var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

//        // Отправляем POST-запрос
//        var response = await httpClient.PostAsync(passengerModuleUrl, content);

//        if (response.IsSuccessStatusCode)
//        {
//            Console.WriteLine("Available flights sent successfully.");
//        }
//        else
//        {
//            Console.WriteLine($"Error sending available flights: {response.StatusCode}");
//        }
//    }
//}
#endregion

/// Дефолтный эндпоинт
app.MapGet("/", async context =>
{
    //Console.WriteLine("Welcome to the Ticket Office / Check-In module!");
    ////await context.Response.WriteAsync("Welcome to the Ticket Office / Check-In module!");

    //var fl = GetFlightByID(11,Flights);
    //RegisteredPassengers.Add(new PassengerEntry(1112,11));
    //RegisteredPassengers.Add(new PassengerEntry(1113, 11));
    //var registeredPassengers = GetRegisteredPassengersByFlight(11);
    //Baggage.Add(new BaggageInfo(3,11));
    //Baggage.Add(new BaggageInfo(4, 11));
    //var bagginf = GetBaggageByFlight(11);

    //// Формируем данные для uno в нужном формате
    //var unoData = new
    //{
    //    planeId = 11,
    //    passengers = registeredPassengers,
    //    food = registeredPassengers.Count,
    //    baggage = bagginf
    //};

    //SendRegistrationCompletionData(fl);



});

app.MapPost("/a", async context =>
{
var request = await context.Request.ReadFromJsonAsync<UnoD>();
Console.WriteLine( $"{request.planeId}, {request.baggage}, {request.food}");
    List<Pass> passList = request.passengers;
    foreach (var obj in request.passengers)
    {
        Pass i = obj;
        Console.WriteLine(i.passengerId);
    }
}
);

app.MapPost("/check-in/end/{id:int}", async context => ///!!!!!!
{
    // Получаем flightId из маршрута
    if (!int.TryParse(context.Request.RouteValues["id"]?.ToString(), out int flightId))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Invalid Flight ID. Flight ID must be an integer.");
        return;
    }
 
    var flight = GetFlightByID(flightId, Flights);
    if (flight != null)
    {
        flight.RegistrationState = 2;
        Console.WriteLine($"Check-in for flight {flightId} is over.");
        SendRegistrationCompletionData(flight);
    }
}
);

app.MapPost("/check-in/start/{id:int}", async context => ///!!!!!!
{
    // Получаем flightId из маршрута
    if (!int.TryParse(context.Request.RouteValues["id"]?.ToString(), out int flightId))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Invalid Flight ID. Flight ID must be an integer.");
        return;
    }
    var checkinEnd = await context.Request.ReadFromJsonAsync<DateTime>();
    var flight = GetFlightByID(flightId, Flights);
    if (flight != null)
    {
        flight.checkinendTime = checkinEnd;
        flight.RegistrationState = 1;
        Console.WriteLine($"Check-in for flight {flightId} is open.");
    }
}
);

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

    var existingFlight = Flights.FirstOrDefault(f => f.FlightId == newFlight.FlightId);
    if (existingFlight == null)
    {
        Flights.Add(newFlight);
        Baggage.Add(new BaggageInfo(0, newFlight.FlightId));
        Console.WriteLine($"New flight added: ID {newFlight.FlightId}. \nTicket purchase and return services for this flight are available.");
    }
    else
    {
        // Обновляем существующий рейс (если нужно)
        existingFlight.RegistrationState = newFlight.RegistrationState;
        existingFlight.seatsAvailable = newFlight.seatsAvailable;
        Console.WriteLine($"Flight updated: ID {newFlight.FlightId}");
    }

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync("Flights updated successfully.");
});

app.MapGet("/ticket-office/available-flights", () =>
{
    // Возвращаем список доступных рейсов с дополнительной информацией
    var availableFlights = Flights.Select(f => new
    {
        FlightId = f.FlightId, // Убедитесь, что это поле есть
        AirplaneID = f.AirplaneID, // Убедитесь, что это поле есть
        RegistrationState = f.RegistrationState, // Убедитесь, что это поле есть
        seatsAvailable = f.seatsAvailable,
        baggageAvailable = f.baggageAvailable
    }).ToList();

    return Results.Ok(availableFlights);
});

// Метод для получения количества заказов еды (заглушка)

#region
//var timer = new System.Timers.Timer(2000); // Проверка каждую минуту симуляции
//timer.Elapsed += async (sender, e) =>
//{
//    try
//    {
//        // Получаем текущее время симуляции
//        //var simulationTime = await GetSimulationTime();

//        // Выводим время симуляции в консоль
//        //Console.WriteLine($"Current simulation time: {simulationTime}");

//        // Проверяем статус регистрации
//        await CheckRegistrationStatus();
//    }
//    catch (Exception ex)
//    {
//        Console.WriteLine($"Error in timer event: {ex.Message}");
//    }
//}; timer.Start();
#endregion

app.Run();