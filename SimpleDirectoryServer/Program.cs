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
                Console.WriteLine(requestJson + "requestJson");

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
        Console.WriteLine(requestJson);
        try
        {
            var request = JsonSerializer.Deserialize<Dictionary<string, object>>(requestJson);

            if (request == null || !request.ContainsKey("method"))
            {
                return JsonSerializer.Serialize(new { Status = "missing method´, missing date" });
            }

            string method = request["method"].ToString().ToLower();

            // Define methods that require a "resource" field
            var methodsRequiringResource = new HashSet<string> { "create", "read", "update", "delete" };

            if (!IsValidUnixTimestamp(request["date"].ToString()))
            {
                return JsonSerializer.Serialize(new { Status = "illegal date" });
            }

            // Check if "resource" is missing for specific methods
            if (methodsRequiringResource.Contains(method) && !request.ContainsKey("resource"))
            {
                return JsonSerializer.Serialize(new { Status = "missing resource" });
            }

            // Handle other method cases or general success response
            if (method == "known_method") // Replace "known_method" with your known valid methods
            {
                return JsonSerializer.Serialize(new { Status = "success", Message = "Request processed successfully" });
            }
            else
            {
                return JsonSerializer.Serialize(new { Status = "illegal method" });
            }
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new { Status = "error", Message = "invalid json format" });
        }
    }
    // Helper method to validate Unix timestamp
    private static bool IsValidUnixTimestamp(string date)
    {
        // Check if date is a valid integer
        if (long.TryParse(date, out long unixTime))
        {
            DateTimeOffset dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTime);
            return unixTime > 0 && dateTime.Year >= 1800 && dateTime.Year <= 2060;
        }
        return false;
    }
}
