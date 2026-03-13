using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace HeatMeetClient
{
    // ---------- Network Message Structure (mirrors server) ----------
    public class NetworkMessage
    {
        public string Command { get; set; } = string.Empty;
        public object? Data { get; set; }
    }

    // Simple session storage for the client
    public static class ClientSession
    {
        public static string? UserId { get; set; }
        public static string? UserName { get; set; }
        public static string? CurrentGroupCode { get; set; }
    }

    class Program
    {
        static void Main()
        {
            try
            {
                Console.WriteLine("=== HEATMEET CLIENT ===");

                // --- Connection setup ---
                string serverIp = "192.168.111.35"; // Change to your server IP
                int port = 8888;

                Console.WriteLine($"Connecting to server at {serverIp}:{port}...");
                Console.WriteLine(new string('-', 60));

                using TcpClient client = new TcpClient(serverIp, port);
                using NetworkStream stream = client.GetStream();

                Console.WriteLine("✅ Connected to HeatMeet server!\n");

                // --- User identification ---
                Console.Write("Enter your name: ");
                ClientSession.UserName = Console.ReadLine() ?? "Anonymous";
                ClientSession.UserId = Guid.NewGuid().ToString(); // Simple local ID

                // --- Main menu loop ---
                bool exit = false;
                while (!exit)
                {
                    Console.WriteLine("\n=== MAIN MENU ===");
                    Console.WriteLine("1. Create a new group");
                    Console.WriteLine("2. Join a group with code");
                    Console.WriteLine("3. View current group info");
                    Console.WriteLine("4. Mark availability (if in group)");
                    Console.WriteLine("5. Vote for a day (if in group)");
                    Console.WriteLine("6. Exit");
                    Console.Write("Choose option: ");
                    string? option = Console.ReadLine();

                    switch (option)
                    {
                        case "1":
                            CreateGroup(stream);
                            break;
                        case "2":
                            JoinGroup(stream);
                            break;
                        case "3":
                            GetGroupInfo(stream);
                            break;
                        case "4":
                            MarkAvailability(stream);
                            break;
                        case "5":
                            VoteForDay(stream);
                            break;
                        case "6":
                            exit = true;
                            break;
                        default:
                            Console.WriteLine("Invalid option. Try again.");
                            break;
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"\n❌ Connection error: {ex.Message}");
                Console.WriteLine($"• Make sure the server is running at {GetLocalIP()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("\nClient disconnected.");
                Console.WriteLine("Press ENTER to exit...");
                Console.ReadLine();
            }
        }

        // --- Helper to send a message and receive response ---
        static NetworkMessage? SendCommand(NetworkStream stream, string command, object? data = null)
        {
            NetworkMessage msg = new NetworkMessage { Command = command, Data = data };
            string jsonString = JsonSerializer.Serialize(msg);
            byte[] dataBytes = Encoding.UTF8.GetBytes(jsonString);

            stream.Write(dataBytes, 0, dataBytes.Length);

            // Read response
            byte[] buffer = new byte[4096];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            return JsonSerializer.Deserialize<NetworkMessage>(responseJson);
        }

        // --- Command Implementations ---
        static void CreateGroup(NetworkStream stream)
        {
            Console.Write("Enter group name: ");
            string groupName = Console.ReadLine() ?? "My Group";

            var data = new
            {
                userName = ClientSession.UserName,
                groupName = groupName
            };

            NetworkMessage? response = SendCommand(stream, "CREATE_GROUP", data);
            if (response?.Data is JsonElement jsonData)
            {
                bool success = jsonData.GetProperty("success").GetBoolean();
                string message = jsonData.GetProperty("message").GetString() ?? "";

                if (success)
                {
                    string groupCode = jsonData.GetProperty("groupCode").GetString() ?? "";
                    ClientSession.CurrentGroupCode = groupCode;
                    Console.WriteLine($"✅ {message}");
                }
                else
                {
                    Console.WriteLine($"❌ {message}");
                }
            }
        }

        static void JoinGroup(NetworkStream stream)
        {
            Console.Write("Enter group code: ");
            string groupCode = Console.ReadLine() ?? "";

            var data = new
            {
                userName = ClientSession.UserName,
                groupCode = groupCode
            };

            NetworkMessage? response = SendCommand(stream, "JOIN_GROUP", data);
            if (response?.Data is JsonElement jsonData)
            {
                bool success = jsonData.GetProperty("success").GetBoolean();
                string message = jsonData.GetProperty("message").GetString() ?? "";

                if (success)
                {
                    ClientSession.CurrentGroupCode = groupCode;
                    Console.WriteLine($"✅ {message}");

                    // Show members
                    var members = jsonData.GetProperty("members").EnumerateArray().Select(m => m.GetString());
                    Console.WriteLine("Members: " + string.Join(", ", members));
                }
                else
                {
                    Console.WriteLine($"❌ {message}");
                }
            }
        }

        static void GetGroupInfo(NetworkStream stream)
        {
            if (string.IsNullOrEmpty(ClientSession.CurrentGroupCode))
            {
                Console.WriteLine("❌ You are not in a group. Create or join one first.");
                return;
            }

            var data = new { groupCode = ClientSession.CurrentGroupCode };
            NetworkMessage? response = SendCommand(stream, "GET_GROUP_INFO", data);

            if (response?.Data is JsonElement jsonData)
            {
                bool success = jsonData.GetProperty("success").GetBoolean();
                if (success)
                {
                    string groupName = jsonData.GetProperty("groupName").GetString() ?? "";
                    string groupCode = jsonData.GetProperty("groupCode").GetString() ?? "";
                    Console.WriteLine($"\n📋 Group: {groupName} ({groupCode})");

                    // Members
                    var members = jsonData.GetProperty("members").EnumerateArray()
                        .Select(m => m.GetProperty("name").GetString());
                    Console.WriteLine("Members: " + string.Join(", ", members));

                    // Availability (simplified view)
                    if (jsonData.TryGetProperty("availability", out JsonElement availability))
                    {
                        Console.WriteLine("\n📅 Availability marked for:");
                        foreach (var date in availability.EnumerateObject())
                        {
                            Console.WriteLine($"   {date.Name}: {date.Value}");
                        }
                    }

                    // Votes
                    if (jsonData.TryGetProperty("votes", out JsonElement votes))
                    {
                        Console.WriteLine("\n🗳️ Votes:");
                        foreach (var vote in votes.EnumerateObject())
                        {
                            Console.WriteLine($"   {vote.Name}: {vote.Value} votes");
                        }
                    }
                }
                else
                {
                    string message = jsonData.GetProperty("message").GetString() ?? "";
                    Console.WriteLine($"❌ {message}");
                }
            }
        }

        static void MarkAvailability(NetworkStream stream)
        {
            if (string.IsNullOrEmpty(ClientSession.CurrentGroupCode))
            {
                Console.WriteLine("❌ You are not in a group.");
                return;
            }

            Console.Write("Enter date (yyyy-MM-dd): ");
            string date = Console.ReadLine() ?? "";
            Console.WriteLine("Select status: 1. Free  2. Busy  3. Very Busy");
            string? statusOption = Console.ReadLine();
            string status = statusOption switch
            {
                "1" => "Free",
                "2" => "Busy",
                "3" => "VeryBusy",
                _ => "Free"
            };

            var data = new
            {
                groupCode = ClientSession.CurrentGroupCode,
                userId = ClientSession.UserId,
                date = date,
                status = status
            };

            NetworkMessage? response = SendCommand(stream, "MARK_AVAILABILITY", data);
            if (response?.Data is JsonElement jsonData)
            {
                bool success = jsonData.GetProperty("success").GetBoolean();
                string message = jsonData.GetProperty("message").GetString() ?? "";
                Console.WriteLine(success ? $"✅ {message}" : $"❌ {message}");
            }
        }

        static void VoteForDay(NetworkStream stream)
        {
            if (string.IsNullOrEmpty(ClientSession.CurrentGroupCode))
            {
                Console.WriteLine("❌ You are not in a group.");
                return;
            }

            Console.Write("Enter date to vote for (yyyy-MM-dd): ");
            string date = Console.ReadLine() ?? "";

            var data = new
            {
                groupCode = ClientSession.CurrentGroupCode,
                userId = ClientSession.UserId,
                date = date
            };

            NetworkMessage? response = SendCommand(stream, "VOTE_DAY", data);
            if (response?.Data is JsonElement jsonData)
            {
                bool success = jsonData.GetProperty("success").GetBoolean();
                string message = jsonData.GetProperty("message").GetString() ?? "";
                Console.WriteLine(success ? $"✅ {message}" : $"❌ {message}");
            }
        }

        static string GetLocalIP()
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "Not available";
        }
    }
}
