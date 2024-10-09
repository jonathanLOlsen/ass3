using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

var port = 5000;
var server = new TcpListener(IPAddress.Loopback, port); // IPv4 127.0.0.1 IPv6 ::1
server.Start();

Console.WriteLine($"Server started on port {port}");

while (true)
{
    var client = server.AcceptTcpClient();
    Console.WriteLine("Client connected!!!");

    try
    {
        var stream = client.GetStream();
        var buffer = new byte[1024];

        // Read data from the client
        int bytesRead = stream.Read(buffer, 0, buffer.Length);

        // Convert received bytes to string
        var msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        Console.WriteLine("Message from client: " + msg);

        // Process the request and get the response
        var responseJson = ProcessRequest(msg);

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
        client.Close(); // Close the client connection
    }
}

// Function to process the client request
string ProcessRequest(string requestJson)
{
    var response = new CJTPResponse();

    try
    {
        // Attempt to deserialize the incoming JSON request
        var request = JsonSerializer.Deserialize<CJTPRequest>(requestJson);

        // Check for missing "method" field
        if (string.IsNullOrWhiteSpace(request?.Method))
        {
            response.Status = 4; // Bad Request
            response.Body = "missing method";
            return JsonSerializer.Serialize(response);
        }

        // Validate known methods
        var knownMethods = new HashSet<string> { "create", "read", "update", "delete", "echo" };
        if (!knownMethods.Contains(request.Method.ToLower()))
        {
            response.Status = 4; // Bad Request
            response.Body = "illegal method";
            return JsonSerializer.Serialize(response);
        }

        // Check for missing resource (path) on specific methods
        if (new[] { "create", "read", "update", "delete" }.Contains(request.Method.ToLower()) &&
            string.IsNullOrWhiteSpace(request.Path))
        {
            response.Status = 4; // Bad Request
            response.Body = "missing resource";
            return JsonSerializer.Serialize(response);
        }

        // If all checks pass, return a generic success response
        response.Status = 1; // OK status for successful processing
        response.Body = "Request processed successfully";
        return JsonSerializer.Serialize(response);
    }
    catch (JsonException)
    {
        // JSON parsing error, return an error response
        response.Status = 6; // Error status
        response.Body = "Error processing request";
        return JsonSerializer.Serialize(response);
    }
    catch (Exception ex)
    {
        // Catch any other exceptions, return a general error response
        Console.WriteLine($"Unexpected error: {ex.Message}");
        response.Status = 6; // Error status
        response.Body = "Unexpected server error";
        return JsonSerializer.Serialize(response);
    }
}

// CJTP Request Model
public class CJTPRequest
{
    public string Method { get; set; }
    public string Path { get; set; }
    public long Date { get; set; }
    public JsonElement? Body { get; set; }
}

// CJTP Response Model
public class CJTPResponse
{
    public int Status { get; set; }
    public string Body { get; set; }
}
