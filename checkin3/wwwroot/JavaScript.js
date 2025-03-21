// Функция для загрузки данных о рейсах
async function loadFlights() {
    debugger; // Принудительная остановка
    try {
        const response = await fetch('/ticket-office/available-flights');
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        const flights = await response.json();

        console.log("Data received from server:", flights); // Логируем данные

        const tableBody = document.querySelector('#flightsTable tbody');
        tableBody.innerHTML = ''; // Очищаем таблицу перед обновлением

        flights.forEach(flight => {
            console.log("Flight object:", flight); // Логируем каждый объект flight
            console.log("Flight ID:", flight.flightId); // Логируем FlightId
            console.log("Airplane ID:", flight.airplaneID); // Логируем AirplaneID
            console.log("Registration State:", flight.registrationState); // Логируем RegistrationState

            const row = document.createElement('tr');

            row.innerHTML = `
                <td>${flight.flightId}</td>
                <td>${flight.airplaneID}</td>
                <td>${getStatusText(flight.registrationState)}</td>
                <td>${flight.seatsAvailable}</td>
                <td>${flight.baggageAvailable}</td>
            `;

            tableBody.appendChild(row);
        });
    } catch (error) {
        console.error('Error loading flights:', error);
    }
}

function getStatusText(status) {
    switch (status) {
        case 0: return 'Purchase/Return Open';
        case 1: return 'Registration Open';
        case 2: return 'Registration Completed';
        default: return 'Unknown';
    }
}


// Загрузка данных при загрузке страницы
document.addEventListener('DOMContentLoaded', loadFlights);

// Обновление данных каждые 5 секунд
setInterval(loadFlights, 2000);