using Classes;
using Newtonsoft.Json;
using System.Text;

const string ApiBaseUrl = "https://localhost:7266";
const string ApiKey = "console-key";
HttpClient client = new HttpClient();
client.BaseAddress = new Uri(ApiBaseUrl);
client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

Console.WriteLine("Project Defense - Console Client");
Console.WriteLine($"Connecting to API at: {ApiBaseUrl}");

await RunMenuAsync();

async Task RunMenuAsync()
{
    while (true)
    {
        Console.WriteLine("\nMenu:");
        Console.WriteLine("1. List available slots");
        Console.WriteLine("2. List students (Get Student IDs)");
        Console.WriteLine("3. Book a slot");
        Console.WriteLine("4. Exit");
        Console.Write("Select an option: ");

        string choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                await ListAvailableSlotsAsync();
                break;
            case "2":
                await ListStudentsAsync();
                break;
            case "3":
                await BookSlotAsync();
                break;
            case "4":
                return;
            default:
                Console.WriteLine("Invalid option, please try again.");
                break;
        }
    }
}

async Task ListAvailableSlotsAsync()
{
    Console.WriteLine("\nFetching available slots...");
    try
    {
        HttpResponseMessage response = await client.GetAsync("/api/slots/available");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error: {response.StatusCode}");
            return;
        }

        string jsonResponse = await response.Content.ReadAsStringAsync();
        var slots = JsonConvert.DeserializeObject<List<AvailableSlotDto>>(jsonResponse);

        if (slots == null || slots.Count == 0)
        {
            Console.WriteLine("No available slots found.");
            return;
        }

        Console.WriteLine("--- Available Slots ---");
        foreach (var slot in slots)
        {
            Console.WriteLine($"ID: {slot.ReservationId} | {slot.StartTime:g} | {slot.LecturerName} | {slot.RoomName}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Connection error: {ex.Message}");
    }
}

async Task ListStudentsAsync()
{
    Console.WriteLine("\nFetching student list...");
    try
    {
        HttpResponseMessage response = await client.GetAsync("/api/students");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error: {response.StatusCode}");
            return;
        }

        string jsonResponse = await response.Content.ReadAsStringAsync();
        var students = JsonConvert.DeserializeObject<List<StudentDto>>(jsonResponse);

        if (students == null || students.Count == 0)
        {
            Console.WriteLine("No students found.");
            return;
        }

        Console.WriteLine("--- Student List (Copy the ID you need) ---");
        foreach (var student in students)
        {
            Console.WriteLine($"ID: {student.Id} | UserName: {student.UserName}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Connection error: {ex.Message}");
    }
}

async Task BookSlotAsync()
{
    Console.Write("Enter your Student ID (use option 2 to find it): ");
    string studentId = Console.ReadLine();

    Console.Write("Enter the Reservation ID you want to book: ");
    if (!int.TryParse(Console.ReadLine(), out int reservationId))
    {
        Console.WriteLine("Invalid ID format.");
        return;
    }

    var requestDto = new BookSlotRequestDto
    {
        StudentId = studentId
    };

    string serializedRequest = JsonConvert.SerializeObject(requestDto);
    StringContent content = new StringContent(serializedRequest, Encoding.UTF8, "application/json");

    Console.WriteLine($"Sending booking request for slot {reservationId}...");

    try
    {
        HttpResponseMessage response = await client.PostAsync($"/api/slots/{reservationId}/book", content);

        string jsonResponse = await response.Content.ReadAsStringAsync();
        dynamic result = JsonConvert.DeserializeObject(jsonResponse);
        string message = result?.message ?? jsonResponse;

        if (response.IsSuccessStatusCode)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Success: {message}");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error ({response.StatusCode}): {message}");
            Console.ResetColor();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Connection error: {ex.Message}");
    }
}