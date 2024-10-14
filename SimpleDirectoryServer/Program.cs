using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

class Program
{

    static List<Dictionary<string, object>> categories = new List<Dictionary<string, object>>()
    {
        new Dictionary<string, object> { { "cid", 1 }, { "name", "Beverages" } },
        new Dictionary<string, object> { { "cid", 2 }, { "name", "Condiments" } },
        new Dictionary<string, object> { { "cid", 3 }, { "name", "Confections" } }
    };
    static readonly object categoryLock = new object();

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

            // Start a new thread for each client connection
            Thread clientThread = new Thread(() => HandleClient(client));
            clientThread.Start();
        }
    }
    static void HandleClient(TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            var buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            if (bytesRead == 0)
            {
                Console.WriteLine("No data received. Closing the connection.");
                client.Close();
                return;
            }

            var requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Console.WriteLine("Message from client: " + requestJson);

            // Process the request and get the response
            var responseJson = ProcessRequest(requestJson);
            Console.WriteLine("Message to client: " + responseJson);

            buffer = Encoding.UTF8.GetBytes(responseJson);
            stream.Write(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
        finally
        {
            client.Close();
            Console.WriteLine("Client connection closed.");
        }
    }

    static string ProcessRequest(string requestJson)
    {
        try
        {
            var requestData = JsonSerializer.Deserialize<Dictionary<string, object>>(requestJson);
            
            // Check if the request data is empty (e.g., "{}")
            if (requestData == null || requestData.Count == 0)
            {
                // Return an error indicating that the "method" is missing
                return JsonSerializer.Serialize(new Response { Status = "4 Bad Request: missing method, missing date", Body = null });
            }

            var response = Validate(requestData);

            if (response.Status.StartsWith("4") || response.Status.StartsWith("6"))
                return JsonSerializer.Serialize(response);

            string method = requestData["method"].ToString().ToLower();
            string path = requestData.ContainsKey("path") ? requestData["path"].ToString() : null;

            switch (method)
            {
                case "create":
                    if (path == "/api/categories")
                        response = CreateCategory(requestData);
                    else
                        response = new Response { Status = "4 Bad Request" };
                    break;

                case "read":
                    response = ReadCategory(path);
                    break;

                case "update":
                    response = UpdateCategory(path, requestData);
                    break;

                case "delete":
                    response = DeleteCategory(path);
                    break;

                case "echo":
                    response.Status = "1 Ok";
                    response.Body = requestData.ContainsKey("body") ? requestData["body"] : null;
                    break;

                default:
                    response.Status = "4 Bad Request: invalid method";
                    break;
            }

            return JsonSerializer.Serialize(response);
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new Response { Status = "4 Bad Request: invalid json format" });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new Response { Status = $"6 Error: unexpected error - {ex.Message}" });
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

        // Special handling for "echo" method: Return the body as-is if it's present, but require body to be there
        if (method == "echo" && !requestData.ContainsKey("body"))
        {
            errors.Add("missing body");
        }
        else if (method == "echo")
        {
            // If "echo" has a body, return it as the response immediately
            var responseBody = requestData["body"];
            return new Response { Status = "1 Ok", Body = responseBody };
        }

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
        if ((method == "create" || method == "update"))
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
    static Response CreateCategory(Dictionary<string, object> requestData)
    {
        lock (categoryLock)
        {
            if (!requestData.ContainsKey("body") || !IsValidJsonObject(requestData["body"].ToString()))
                return new Response { Status = "4 Bad Request: missing or invalid body" };

            var newCategoryData = JsonSerializer.Deserialize<Dictionary<string, object>>(requestData["body"].ToString());
            if (!newCategoryData.ContainsKey("name"))
                return new Response { Status = "4 Bad Request: missing name in body" };

            int newId = categories.Count + 1;
            var newCategory = new Dictionary<string, object> { { "cid", newId }, { "name", newCategoryData["name"] } };
            categories.Add(newCategory);

            return new Response { Status = "2 Created", Body = JsonSerializer.Serialize(newCategory) };
        }
    }

    static Response ReadCategory(string path)
    {
        if (path == "/api/categories")
        {
            // Serialize categories list to JSON string to match expected format
            string categoriesJson = JsonSerializer.Serialize(categories);
            return new Response { Status = "1 Ok", Body = categoriesJson };
        }

        if (path.StartsWith("/api/categories/"))
        {
            var idStr = path.Substring("/api/categories/".Length);
            if (int.TryParse(idStr, out int cid))
            {
                var category = categories.Find(c => (int)c["cid"] == cid);
                if (category != null)
                    return new Response { Status = "1 Ok", Body = JsonSerializer.Serialize(category) };
                return new Response { Status = "5 Not Found" };
            }
        }

        return new Response { Status = "4 Bad Request" };
    }

    static Response UpdateCategory(string path, Dictionary<string, object> requestData)
    {
        lock (categoryLock)
        {
            if (!path.StartsWith("/api/categories/"))
                return new Response { Status = "4 Bad Request" };

            if (!requestData.ContainsKey("body") || !IsValidJsonObject(requestData["body"].ToString()))
                return new Response { Status = "4 Bad Request: missing or invalid body" };

            var idStr = path.Substring("/api/categories/".Length);
            if (int.TryParse(idStr, out int cid))
            {
                var category = categories.Find(c => (int)c["cid"] == cid);
                if (category != null)
                {
                    var updatedCategory = JsonSerializer.Deserialize<Dictionary<string, object>>(requestData["body"].ToString());
                    category["name"] = updatedCategory["name"];
                    return new Response { Status = "3 Updated" };
                }
                return new Response { Status = "5 Not Found" };
            }

            return new Response { Status = "4 Bad Request: invalid id" };
        }
    }
    static Response DeleteCategory(string path)
    {
        lock (categoryLock)
        {
            if (!path.StartsWith("/api/categories/"))
                return new Response { Status = "4 Bad Request" };

            var idStr = path.Substring("/api/categories/".Length);
            if (int.TryParse(idStr, out int cid))
            {
                var category = categories.Find(c => (int)c["cid"] == cid);
                if (category != null)
                {
                    categories.Remove(category);
                    return new Response { Status = "1 Ok" };
                }
                return new Response { Status = "5 Not Found" };
            }

            return new Response { Status = "4 Bad Request: invalid id" };
        }
    }






}



public class Response
{
    public string Status { get; set; }
    public object Body { get; set; }
}
