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
                var errorResponse = new
                {
                    Status = "missing method"
                };
                return JsonSerializer.Serialize(errorResponse);
            }

            string method = request["method"].ToString().ToLower();
            if (method != "known_method") // Replace "known_method" with your valid method(s)
            {
                var errorResponse = new
                {
                    Status = "illegal method"
                };
                return JsonSerializer.Serialize(errorResponse);
            }

            var successResponse = new
            {
                Status = "success",
                Message = "Request processed successfully"
            };
            return JsonSerializer.Serialize(successResponse);
        }
        catch (JsonException)
        {
            var errorResponse = new
            {
                Status = "error",
                Message = "invalid json format"
            };
            return JsonSerializer.Serialize(errorResponse);
        }
    }
}
