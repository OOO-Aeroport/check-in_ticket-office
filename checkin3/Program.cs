using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Globalization; // ��� CultureInfo
using System.Threading.Tasks;
using TicketOffice_CheckIn_Module;


var builder = WebApplication.CreateBuilder(args);

//��������� Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5555); // ������� ��� IP-������ �� ����� 5555
});
var app = builder.Build();


//URL �������
string DepartureBoardUrl = "26.228.200.110:5555";
string CateringServiceUrl = "";
string LuggageServiceUrl = "26.132.135.106:5555";
string PassengerModuleUrl = "26.49.89.37:5555";


// ��������� ������
List<PassengerEntry> RegisteredPassengers = new List<PassengerEntry>();
List<int> BuyerIDs = new List<int>();
List<Flight> Flights = new List<Flight>();
List<BaggageInfo> Baggage = new List<BaggageInfo>();
List<FoodOrder> foodOrders = new List<FoodOrder>();



// ��������� �������� ������� ���������
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

                // �������� ���������� ������
                //Console.WriteLine($"Response data: {responseData}");

                // ������� ������ ������� (��������, �������)
                responseData = responseData.Trim('"');

                // ���������, ��� ������ �� ������
                if (string.IsNullOrEmpty(responseData))
                {
                    Console.WriteLine("Response data is empty or null.");
                    throw new Exception("Empty or null response data.");
                }

                // ������ ������ � �������������� ����������� �������
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

// �������� ���������� ����������� ��� ���� ������ 
async Task CheckRegistrationStatus()
{
    var simulationTime = await GetSimulationTime();
    foreach (var flight in Flights)
    {
        if (!flight.IsRegistrationOpen && simulationTime >= flight.departureTime.AddHours(-3))
        {
            // ��������� �����������
            flight.IsRegistrationOpen = true;
            Console.WriteLine($"Check-in for flight {flight.id} is open.");
        }
        if (flight.IsRegistrationOpen && simulationTime >= flight.departureTime.AddMinutes(-30))
        {
            // ��������� �����������
            flight.IsRegistrationOpen = false;
            Console.WriteLine($"Check-in for flight {flight.id} closed.");

            // ���������� ������ � �����, ������ ������� � ������ ������
            await SendRegistrationCompletionData(flight);
        }
    }
}

// �������� ������ � ���������� �����������
async Task SendRegistrationCompletionData(Flight flight)
{
    // �������� ������ ������������������ �������
    var registeredPassengers = GetRegisteredPassengersByFlight(flight.id);
    // �������� ������ ������� ���
    var foodOrdersForFlight = GetFoodOrderByFlight(flight.id);
    // �������� ������ ������
    var baggageForFlight = GetBaggageByFlight(flight.id);

    // ���������� ������ � �����
    string departureBoardUrl = $"http://{DepartureBoardUrl}/departure-board/flights/{flight.id}/passengers";
    await SendDataToService(departureBoardUrl, new { registeredPassengers });
    Console.WriteLine($"Departure table data sent successfully.");

    // ��������� ������ ��� ������ ������� � ������ �������
    var cateringData = new
    {
        list_key = foodOrdersForFlight.Select(order => new
        {
            flight_id = flight.id,
            quantity = order.quantity
        }).ToList()
    };

    // ���������� ������ � ������ �������
    string cateringServiceUrl = $"http://{CateringServiceUrl}/food-orders";
    await SendDataToService(cateringServiceUrl, cateringData);
    Console.WriteLine($"Catering Service data sent successfully.");

    // ���������� ������ � ������ ������
    string luggageServiceUrl = $"http://{LuggageServiceUrl}/transportation-bagg";
    await SendDataToService(luggageServiceUrl, new { FlightID = flight.id, Baggage = baggageForFlight });
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
        if (id == foodOrders[i].flight_id)
        {
            foodOrders[i].quantity++;
        }
    }
}

//����� ������������������ ���������� �� �����
List<object> GetRegisteredPassengersByFlight(int flightID)
{
    List<object> res = new List<object>();
    for (int i = 0; i < RegisteredPassengers.Count; i++)
    {
        if (RegisteredPassengers[i].flight_id == flightID) res.Add(new { passengerId = RegisteredPassengers[i].passenger_id });
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
    // ��������� ������ �� flightID
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

//��������� ����� �� ������ �� ID
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
        Console.WriteLine(psg);

        var flight = GetFlightByID(psg.flight_id, Flights);
        Console.WriteLine(flight);

        string passengerModuleUrl = $"http://{PassengerModuleUrl}/passenger/ticket";

        if (flight == null)
        {
            Console.WriteLine("Impossible to buy a ticket for this flight.");
            lpr.Add(new PassengerResponse(psg.passenger_id, "Unsuccessful"));
            continue;
        }

        // �������� ������� �������
        var simulationTime = await GetSimulationTime();
        if (simulationTime >= flight.departureTime.AddHours(-3)) // ������� ������������� �� 3 ���� �� ������
        {
            Console.WriteLine("Ticket sale for this flight is closed.");
            lpr.Add(new PassengerResponse(psg.passenger_id, "Unsuccessful"));
            continue;
        }

        if (!flight.IsSuitable(psg.baggage_weight))
        {
            Console.WriteLine("The flight is unsuitable.");
            lpr.Add(new PassengerResponse(psg.passenger_id, "Unsuccessful"));
            continue;
        }

        // ������� �����
        Baggage.Add(new BaggageInfo(psg.passenger_id, psg.baggage_weight, flight.id));
        BuyerIDs.Add(psg.passenger_id);
        flight.seatsAvailable--;
        lpr.Add(new PassengerResponse(psg.passenger_id, "Successful"));

        Console.WriteLine($"Ticket for passenger {psg.passenger_id} sold successfully. Seats left: {flight.seatsAvailable}");
    }

    // �������� ������� ������� ��� ���� ����������
    await SendPurchaseStatus(lpr, $"http://{PassengerModuleUrl}/passenger/ticket");

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsJsonAsync(lpr);
});

// �������� ������� ������� ��� ������ ����������
async Task SendPurchaseStatus(List<PassengerResponse> responses, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        // ������� ������ � ID ���������� � �� ���������
        var purchaseData = responses.Select(r => new { PassengerId = r.PassengerID, Status = r.Status }).ToList();
        var purchaseJson = JsonSerializer.Serialize(purchaseData);
        var content = new StringContent(purchaseJson, Encoding.UTF8, "application/json");

        // ���������� POST-������
        var response = await httpClient.PostAsync(passengerModuleUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("������ ������� ������� ��������� ��� ���� ����������.");
        }
        else
        {
            Console.WriteLine($"������: {response.StatusCode}");
        }
    }
}

// ����������� ���������
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

        if (flight == null || !flight.IsRegistrationOpen)
        {
            Console.WriteLine("Check-in for the flight is closed or the flight doesn't exist.");
            responses.Add(new PassengerResponse(psg.passenger_id, "Unsuccessful"));
            continue;
        }

        // �������� ������� �����������
        var simulationTime = await GetSimulationTime();
        if (simulationTime >= flight.departureTime.AddMinutes(-30)) // ����������� ������������� � ��������� �����
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
        // ������������ ���������
        RegisteredPassengers.Add(new PassengerEntry(psg.passenger_id, psg.flight_id));
        AddFoodOrder(flight.id);

        // ��������� �������� �����
        responses.Add(new PassengerResponse(psg.passenger_id, "Successful"));

        Console.WriteLine($"Passenger {passengerId} registered successfully.");
    }

    // �������� ������� ����������� ��� ���� ����������
    await SendPassengerRegistrationStatus(responses, $"http://{PassengerModuleUrl}/passenger/check-in");

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsJsonAsync(responses);
});

// �������� ������� ����������� ��� ������ ����������
async Task SendPassengerRegistrationStatus(List<PassengerResponse> responses, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        // ������� ������ � ID ���������� � �� ���������
        var registrationData = responses.Select(r => new { PassengerId = r.PassengerID, Status = r.Status }).ToList();
        var registrationJson = JsonSerializer.Serialize(registrationData);
        var content = new StringContent(registrationJson, Encoding.UTF8, "application/json");

        // ���������� POST-������
        var response = await httpClient.PostAsync(passengerModuleUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("������ ����������� ������� ��������� ��� ���� ����������.");
        }
        else
        {
            Console.WriteLine($"������: {response.StatusCode}");
        }
    }
}


// ������� ������
app.MapPost("/ticket-office/return-ticket", async context =>
{
    var request = await context.Request.ReadFromJsonAsync<List<PassengerEntry>>();
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

        if (!IfBuyer(psg.passenger_id))
        {
            Console.WriteLine("Ticket not found or forged.");
            responses.Add(new PassengerResponse(psg.passenger_id, "Unsuccessful"));
            continue;
        }

        var flight = GetFlightByID(psg.flight_id, Flights);
        if (flight == null)
        {
            Console.WriteLine("Flight not found.");
            responses.Add(new PassengerResponse(psg.passenger_id, "Unsuccessful"));
            continue;
        }

        var simulationTime = await GetSimulationTime();
        if (simulationTime >= flight.departureTime.AddHours(-3)) // ������� �� 3 ���� �� ������
        {
            Console.WriteLine("Unable to return ticket: less than 3 hours before departure.");
            responses.Add(new PassengerResponse(psg.passenger_id, "Unsuccessful"));
            continue;
        }

        // ������� ������
        BuyerIDs.Remove(psg.passenger_id);
        flight.seatsAvailable++;
        resp = new PassengerResponse(psg.passenger_id, "Successful");
        responses.Add(resp);

        Console.WriteLine($"Ticket for passenger {psg.passenger_id} returned successfully.");
    }

    // �������� ������� �������� ��� ���� ����������
    await SendReturnStatus(responses, $"http://{PassengerModuleUrl}/passenger/return-ticket");

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsJsonAsync(responses);
});

// �������� ������� �������� ��� ������ ����������
async Task SendReturnStatus(List<PassengerResponse> responses, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {
        // ������� ������ � ID ���������� � �� ���������
        var returnData = responses.Select(r => new { PassengerId = r.PassengerID, Status = r.Status }).ToList();
        var returnJson = JsonSerializer.Serialize(returnData);
        var content = new StringContent(returnJson, Encoding.UTF8, "application/json");

        // ���������� POST-������
        var response = await httpClient.PostAsync(passengerModuleUrl, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("������ �������� ������� ��������� ��� ���� ����������.");
        }
        else
        {
            Console.WriteLine($"������: {response.StatusCode}");
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

///// �������� ��� ������ ������ ����� �������� �������
//app.MapGet("/ticket-office/available-flights", async context =>
//{
//    var simulationTime = await GetSimulationTime();
//    //List<FlightInfo> availableFlights = GetAvailableFlights(simulationTime); //������ ������ �� ������� ����� ������ ������
//    List<FlightInfo> availableFlights = new List<FlightInfo>();
//    availableFlights.Add(new FlightInfo(111,DateTime.Now.AddHours(-3).ToString(),DateTime.Now.ToString()));
//    availableFlights.Add(new FlightInfo(222, DateTime.Now.AddHours(-3).ToString(), DateTime.Now.ToString()));
//    Console.WriteLine("done");
//    string dep = $"http://{PassengerModuleUrl}/passenger/available-flights";

//    await context.Response.WriteAsJsonAsync(availableFlights);
//});

/// �������� ��� ������ ������ ����� �������� �������
app.MapGet("/ticket-office/available-flights", async context =>
{
    var simulationTime = await GetSimulationTime();
    List<FlightInfo> availableFlights = GetAvailableFlights(simulationTime); //������ ������ �� ������� ����� ������ ������
    // ������� ������ ��������� ������
    //List<FlightInfo> availableFlights = new List<FlightInfo>
    //{
    //    new FlightInfo(111, simulationTime.AddHours(-3).ToString(), DateTime.Now.ToString()),
    //    new FlightInfo(222, DateTime.Now.AddHours(-3).ToString(), DateTime.Now.ToString())
    //};

    Console.WriteLine("Available flights retrieved.");

    // ���������� ������ �� ��������� ��������
    string passengerModuleUrl = $"http://{PassengerModuleUrl}/passenger/available-flights";
    await SendAvailableFlights(availableFlights, passengerModuleUrl);
    Console.WriteLine("yay");
    // ���������� ������ ������ � ������
    await context.Response.WriteAsJsonAsync(availableFlights);
});

/// ����� ��� �������� ������ ��������� ������
async Task SendAvailableFlights(List<FlightInfo> flights, string passengerModuleUrl)
{
    using (var httpClient = new HttpClient())
    {

        Flights.Add(new Flight(7, (await GetSimulationTime()).AddHours(4), false, 100, 100));
        Flights.Add(new Flight(6, (await GetSimulationTime()).AddHours(5), false, 100, 100));
        Flights.Add(new Flight(5, (await GetSimulationTime()).AddHours(4), false, 100, 100));
        Flights.Add(new Flight(4, (await GetSimulationTime()).AddHours(4), false, 100, 100));

        // ����������� ������ ������ � JSON
        var jsonData = JsonSerializer.Serialize(flights);
        var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

        // ���������� POST-������
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

/// ��������� ��������
app.MapGet("/", async context =>
{
    Console.WriteLine("Welcome to the Ticket Office / Check-In module!");
    await context.Response.WriteAsync("Welcome to the Ticket Office / Check-In module!");
    //PassengerResponse resp;
    List<object> passengers = new List<object>();
    //List<BaggageInfo> bg = new List<BaggageInfo>();
    //List<FoodOrder> fo = new List<FoodOrder>();
    passengers.Add(new { passengerId = 1112 });
    passengers.Add(new { passengerId = 7786 });
    //bg.Add(new BaggageInfo(111, 3,222));
    //bg.Add(new BaggageInfo(444, 5, 333));
    //fo.Add(new FoodOrder(111));
    //fo.Add(new FoodOrder(333));
    // ��������� ������ ��� ������ ������� � ������ �������
    //var cateringData = new
    //{
    //    list_key = fo.Select(order => new
    //    {
    //        flight_id = order.flight_id,
    //        quantity = 22
    //    }).ToList()
    //};
    await SendDataToService($"http://{DepartureBoardUrl}/departure-board/flights/{7}/passengers", passengers);
    //await context.Response.WriteAsJsonAsync(rp);

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

    var existingFlight = Flights.FirstOrDefault(f => f.id == newFlight.id);
    if (existingFlight == null)
    {
        Flights.Add(newFlight);
        Console.WriteLine($"New flight added: ID {newFlight.id}, Departure: {newFlight.departureTime}");
    }
    else
    {
        // ��������� ������������ ���� (���� �����)
        existingFlight.departureTime = newFlight.departureTime;
        existingFlight.IsRegistrationOpen = newFlight.IsRegistrationOpen;
        existingFlight.seatsAvailable = newFlight.seatsAvailable;
        Console.WriteLine($"Flight updated: ID {newFlight.id}");
    }

    context.Response.StatusCode = StatusCodes.Status200OK;
    await context.Response.WriteAsync("Flights updated successfully.");
});

var timer = new System.Timers.Timer(2000); // �������� ������ ������ ���������
timer.Elapsed += async (sender, e) =>
{
    try
    {
        // �������� ������� ����� ���������
        //var simulationTime = await GetSimulationTime();

        // ������� ����� ��������� � �������
        //Console.WriteLine($"Current simulation time: {simulationTime}");

        // ��������� ������ �����������
        await CheckRegistrationStatus();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in timer event: {ex.Message}");
    }
}; timer.Start();

app.Run();