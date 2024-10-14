using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

class Program
{
    static void Main(string[] args)
    {
        var port = 5000;
        var server = new TcpListener(IPAddress.Loopback, port);
        server.Start();

        Console.WriteLine($"Server started on port {port}");

        while (true)
        {
            var client = server.AcceptTcpClient();
            Console.WriteLine("Client connected!");

            try
            {
                var stream = client.GetStream();
                var buffer = new byte[1024];

                // Read data from the client
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                // Check if no data is received (client connected without sending data)
                if (bytesRead == 0)
                {
                    Console.WriteLine("No data received. Closing the connection.");
                    client.Close();
                    continue;
                }

                // Convert received bytes to string
                var requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine("Message from client: " + requestJson);

                // Process the request and get the response
                var responseJson = ProcessRequest(requestJson);
                Console.WriteLine("Message to client: " + responseJson);

                // Convert the response to bytes and send it back to the client
                buffer = Encoding.UTF8.GetBytes(responseJson);
                stream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            finally
            {
                client.Close(); // Ensure the client connection is closed
                Console.WriteLine("Client connection closed."); // Log message when connection closes
            }
        }
    }

    static string ProcessRequest(string requestJson)
    {
        try
        {
            // Parse JSON into a Dictionary
            var requestData = JsonSerializer.Deserialize<Dictionary<string, object>>(requestJson);

            // Check if the request data is empty (e.g., "{}")
            if (requestData == null || requestData.Count == 0)
            {
                Console.WriteLine("Received empty request data.");
                return JsonSerializer.Serialize(new Response { Status = "4 Bad Request: missing method, missing date", Body = null });
            }

            // Debugging: Check parsed request data
            Console.WriteLine("Parsed request data: " + JsonSerializer.Serialize(requestData));

            // Validate the request data
            var response = Validate(requestData);

            // Return the response as a JSON string
            return JsonSerializer.Serialize(response);
        }
        catch (JsonException)
        {
            // Handle invalid JSON format
            var errorResponse = new Response { Status = "4 Bad Request: invalid json format", Body = null };
            return JsonSerializer.Serialize(errorResponse);
        }
        catch (Exception ex)
        {
            // Handle unexpected errors
            var errorResponse = new Response { Status = $"6 Error: unexpected error - {ex.Message}", Body = null };
            return JsonSerializer.Serialize(errorResponse);
        }
    }

    // Validate fields and accumulate error messages
    public static Response Validate(Dictionary<string, object> requestData)
    {
        var errors = new List<string>();

        // Check if "method" is present and valid
        if (!requestData.ContainsKey("method") || string.IsNullOrWhiteSpace(requestData["method"]?.ToString()))
        {
            errors.Add("missing method");
        }
        else if (!IsValidMethod(requestData["method"].ToString()))
        {
            errors.Add("illegal method");
        }
        string method = requestData["method"]?.ToString().ToLower();

        // Check if "path" (resource) is required and present for certain methods
        if ((method == "create" || method == "read" || method == "update" || method == "delete") &&
            !requestData.ContainsKey("path"))
        {
            errors.Add("missing resource");
        }

        // Check if "date" is present and is a valid Unix timestamp
        if (!requestData.ContainsKey("date") ||
            !long.TryParse(requestData["date"]?.ToString(), out long parsedDate) || // Rename date to parsedDate
            !IsValidUnixTimestamp(parsedDate))
        {
            errors.Add("illegal date");
        }



        // Check if "path" is present, required for non-echo methods
        if (!requestData.ContainsKey("path") && requestData["method"]?.ToString().ToLower() != "echo")
        {
            errors.Add("missing path");
        }

        // Check if "date" is present and valid
        if (!requestData.ContainsKey("date") || !long.TryParse(requestData["date"]?.ToString(), out long date) || !IsValidUnixTimestamp(date))
        {
            errors.Add("missing date or illegal date");
        }

        // Check if "body" is required for specific methods and is present
        if ((method == "create" || method == "update" || method == "echo"))
        {
            if (!requestData.ContainsKey("body"))
            {
                errors.Add("missing body");
            }
            else if (!IsValidJsonObject(requestData["body"].ToString()))
            {
                errors.Add("illegal body");
            }
        }

        // Return a "4 Bad Request" response with errors if any are found
        if (errors.Count > 0)
        {
            return new Response { Status = $"4 Bad Request: {string.Join(", ", errors)}", Body = null };
        }

        // Return a success response if no errors
        return new Response { Status = "1 Ok", Body = null };
    }

    // Check if the Unix timestamp is valid
    public static bool IsValidUnixTimestamp(long timestamp)
    {
        // Unix timestamps start from January 1, 1970
        DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(timestamp);

        // Check if the timestamp is within a valid range (e.g., between 1970 and 2100)
        return dateTimeOffset.Year >= 1970 && dateTimeOffset.Year <= 2100;
    }

    public static bool IsValidMethod(string method)
    {
        var validMethods = new HashSet<string> { "create", "read", "update", "delete", "echo" };
        return validMethods.Contains(method.ToLower());
    }
    public static bool IsValidJsonObject(string body)
    {
        try
        {
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(body);
            return jsonElement.ValueKind == JsonValueKind.Object; // Ensure it's a JSON object
        }
        catch
        {
            return false; // If deserialization fails, it's not a valid JSON object
        }
    }
}



public class Response
{
    public string Status { get; set; }
    public object Body { get; set; }
}
