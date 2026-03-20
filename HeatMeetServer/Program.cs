using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SharedModels;

namespace HeatMeetServer
{
    // ---------- Data Models ----------
    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
    }

    public class Group
    {
        public string GroupCode { get; set; } = GenerateGroupCode();
        public string GroupName { get; set; } = string.Empty;
        public User Admin { get; set; } = new User();
        public List<User> Members { get; set; } = new();
        public Dictionary<string, CalendarDay> Availability { get; set; } = new(); // Key: "yyyy-MM-dd"
        public Dictionary<string, int> Votes { get; set; } = new(); // Key: "yyyy-MM-dd", Value: vote count

        private static string GenerateGroupCode()
        {
            //should also make a database check if it exists?
            return Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
        }
    }

    public class CalendarDay
    {
        public string Date { get; set; } = string.Empty; // Format: yyyy-MM-dd
        public Dictionary<string, string> UserStatus { get; set; } = new(); // Key: UserId, Value: "Free", "Busy", "VeryBusy"
    }

    // ---------- Network Message Structure ----------
    //public class NetworkMessage
    //{
    //    public string Command { get; set; } = string.Empty; // e.g., "CREATE_GROUP", "JOIN_GROUP", "MARK_AVAILABILITY", "VOTE_DAY", "GET_GROUP_INFO"
    //    public object? Data { get; set; }
    //}
    public static class AuthService
    {
        public static (bool success, string message, int userId, string userName) 
                  Login(OrmManager db, string email, string password)
        {
            try
            {
                var user = db.Users.FirstOrDefault(u => u.Email == email);

                if (user == null)
                    return (false, "Usuario no existe", 0, "");

                if (user.Password != password)
                    return (false, "Contraseña incorrecta", 0, "");

                return (true, "Login correcto", user.Id, user.Name);
            }
            catch (Exception ex)
            {
                return (false, $"Error en servidor: {ex.Message}", 0, "");
            }
        }
    }

    public class Program
    {
        // In-memory storage (simulates a database)
        public static List<Group> ActiveGroups = new List<Group>();
        public static OrmManager ormManager {  get; private set; }

        static void Main(string[] args)
        {

            ormManager = new OrmManager();
            ormManager.Database.EnsureCreated();
            
            //Create test users and groups

            //Test users
            Users? existsUsers = ormManager.Users.FirstOrDefault(u => u.Email == "jorge1@gmail.com");
            if (existsUsers == null)
            {
                Users? newUser = new Users
                {
                    Name = "JORGE",
                    Email = "jorge1@gmail.com",
                    Password = "123"
                };
                ormManager.Users.Add(newUser);   
                Users? newUser2 = new Users
                {
                    Name = "admin",
                    Email = "admin",
                    Password = "admin"//this should not exist once the app launches
                };
                ormManager.Users.Add(newUser2);

               
                ormManager.SaveChanges();
                Console.WriteLine("Test users added.");
            }
            else Console.WriteLine("Test users already exists.");

            //Test groups
            string groupName = "TestGroup";
            Groups? currentGroup = ormManager.Groups.Include(g => g.Users).FirstOrDefault(g => g.Name == groupName);
            if (currentGroup == null)
            {
                currentGroup = new Groups
                {
                    Name = groupName,
                    InviteCode = "ABC123",
                    CreateDate = DateTime.UtcNow
                };
                ormManager.Groups.Add(currentGroup);
                ormManager.SaveChanges();
                Console.WriteLine("Test group created.");
            }

            Users? userJorge = ormManager.Users.FirstOrDefault(u => u.Name == "JORGE");
            Users? userAdmin = ormManager.Users.FirstOrDefault(u => u.Name == "admin");

            bool added = false;
            if (userJorge != null && !currentGroup.Users.Any(u => u.Id == userJorge.Id))
            {
                currentGroup.Users.Add(userJorge);
                added = true;
            }

            if (userAdmin != null && !currentGroup.Users.Any(u => u.Id == userAdmin.Id))
            {
                currentGroup.Users.Add(userAdmin);
                added = true;
            }

            if (added)
            {
                ormManager.SaveChanges();
                Console.WriteLine("New users linked to TestGroup.");
            }
            else
            {
                Console.WriteLine("Users were already in TestGroup.");
            }


            //infinite client accept loop
            try
            {
                Console.WriteLine("=== HEATMEET TCP SERVER ===");

                IPAddress address = IPAddress.Any;//<--In a near future we will put a good ip selector hoster with dns and more
                int port = 8888;

                Console.WriteLine("Local IPs available:");
                ShowLocalIPs();

                TcpListener server = new TcpListener(address, port);
                server.Start();

                Console.WriteLine($"\n✅ Server listening on ip {address}(all ip's) on port {port}");//for some reason this gives ip 0.0.0.0, that is a bug?
                Console.WriteLine(new string('-', 60));

                while (true) // Main loop to accept multiple clients
                {
                    TcpClient client = server.AcceptTcpClient();
                    IPEndPoint? clientEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                    Console.WriteLine($"✅ Client connected from: {clientEndPoint?.Address}:{clientEndPoint?.Port}");

                    // Handle each client in a separate thread
                    Thread clientThread = new Thread(HandleClient);
                    clientThread.Start(client);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
            }
        }



        static void HandleClient(object? obj)
        {
            if (obj is not TcpClient client) return;

            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[4096]; // Larger buffer for JSON

                    while (true)
                    {
                        // Read message
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break; // Client disconnected

                        string jsonString = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine($"📩 Received: {jsonString}");

                        // Deserialize message
                        NetworkMessage? message = JsonSerializer.Deserialize<NetworkMessage>(jsonString);
                        if (message == null) continue;

                        // Process command
                        NetworkMessage response = ProcessCommand(message);

                        // Send response
                        string responseJson = JsonSerializer.Serialize(response);
                        byte[] responseData = Encoding.UTF8.GetBytes(responseJson);
                        stream.Write(responseData, 0, responseData.Length);
                        Console.WriteLine($"📤 Sent: {responseJson}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Client handler error: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("🔌 Client disconnected");
            }
        }

        static NetworkMessage ProcessCommand(NetworkMessage message)
        {
            NetworkMessage response = new NetworkMessage { Command = message.Command };

            try
            {
                switch (message.Command)
                {
                    case "CREATE_GROUP":
                        // Expected Data: JsonElement with { "userName": "...", "groupName": "..." }
                        if (message.Data is JsonElement createData)
                        {
                            string userName = createData.GetProperty("userName").GetString() ?? "Anonymous";
                            string groupName = createData.GetProperty("groupName").GetString() ?? "New Group";

                            User admin = new User { Name = userName };
                            Group newGroup = new Group
                            {
                                GroupName = groupName,
                                Admin     = admin
                            };
                            newGroup.Members.Add(admin);

                            ActiveGroups.Add(newGroup);

                            response.Data = new
                            {
                                success   = true,
                                groupCode = newGroup.GroupCode,
                                message   = $"Group '{groupName}' created successfully. Share code: {newGroup.GroupCode}"
                            };
                        }
                        else
                        {
                            response.Data = new { success = false, message = "Invalid data format" };
                        }
                        break;

                    case "JOIN_GROUP":
                        // Expected Data: { "userName": "...", "groupCode": "..." }
                        if (message.Data is JsonElement joinData)
                        {
                            string userName = joinData.GetProperty("userName").GetString() ?? "Anonymous";
                            string groupCode = joinData.GetProperty("groupCode").GetString() ?? "";

                            Group? group = ActiveGroups.FirstOrDefault(g => g.GroupCode == groupCode);
                            if (group != null)
                            {
                                User newMember = new User { Name = userName };
                                if (!group.Members.Any(m => m.Id == newMember.Id))
                                {
                                    group.Members.Add(newMember);
                                }

                                response.Data = new
                                {
                                    success = true,
                                    groupName = group.GroupName,
                                    members = group.Members.Select(m => m.Name).ToList(),
                                    message = $"Joined group '{group.GroupName}' successfully"
                                };
                            }
                            else
                            {
                                response.Data = new { success = false, message = "Group not found" };
                            }
                        }
                        else
                        {
                            response.Data = new { success = false, message = "Invalid data format" };
                        }
                        break;

                    case "MARK_AVAILABILITY":
                        // Expected Data: { "groupCode": "...", "userId": "...", "date": "yyyy-MM-dd", "status": "Free/Busy/VeryBusy" }
                        if (message.Data is JsonElement availData)
                        {
                            string groupCode = availData.GetProperty("groupCode").GetString() ?? "";
                            string userId = availData.GetProperty("userId").GetString() ?? "";
                            string date = availData.GetProperty("date").GetString() ?? "";
                            string status = availData.GetProperty("status").GetString() ?? "Free";

                            Group? group = ActiveGroups.FirstOrDefault(g => g.GroupCode == groupCode);
                            if (group != null)
                            {
                                if (!group.Availability.ContainsKey(date))
                                {
                                    group.Availability[date] = new CalendarDay { Date = date };
                                }

                                group.Availability[date].UserStatus[userId] = status;

                                response.Data = new
                                {
                                    success = true,
                                    message = $"Availability for {date} updated to {status}"
                                };
                            }
                            else
                            {
                                response.Data = new { success = false, message = "Group not found" };
                            }
                        }
                        else
                        {
                            response.Data = new { success = false, message = "Invalid data format" };
                        }
                        break;

                    case "LOGIN":
                        if (message.Data is JsonElement loginData)
                        {
                            string email = loginData.GetProperty("email").GetString() ?? "";
                            string password = loginData.GetProperty("password").GetString() ?? "";

                            var result = AuthService.Login(ormManager, email, password);

                            response.Data = new
                            {
                                success = result.success,
                                message = result.message,
                                userId = result.userId,
                                userName = result.userName
                            };
                        }
                        else
                        {
                            response.Data = new { success = false, message = "Datos inválidos" };
                        }
                        break;

                    case "VOTE_DAY":
                        //Expected Data: { "groupCode": "...", "userId": "...", "date": "yyyy-MM-dd" }
                        if (message.Data is JsonElement voteData)
                        {
                            string groupCode = voteData.GetProperty("groupCode").GetString() ?? "";
                            string userId = voteData.GetProperty("userId").GetString() ?? "";
                            string date = voteData.GetProperty("date").GetString() ?? "";

                            Group? group = ActiveGroups.FirstOrDefault(g => g.GroupCode == groupCode);
                            if (group != null)
                            {
                                if (!group.Votes.ContainsKey(date))
                                {
                                    group.Votes[date] = 0;
                                }
                                group.Votes[date]++;

                                response.Data = new
                                {
                                    success = true,
                                    message = $"Vote for {date} recorded. Total votes: {group.Votes[date]}"
                                };
                            }
                            else
                            {
                                response.Data = new { success = false, message = "Group not found" };
                            }
                        }
                        else
                        {
                            response.Data = new { success = false, message = "Invalid data format" };
                        }
                        break;

                    case "GET_GROUP_INFO":
                        // Expected Data: { "groupCode": "..." }
                        if (message.Data is JsonElement infoData)
                        {
                            string groupCode = infoData.GetProperty("groupCode").GetString() ?? "";
                            Group? group = ActiveGroups.FirstOrDefault(g => g.GroupCode == groupCode);
                            if (group != null)
                            {
                                response.Data = new
                                {
                                    success = true,
                                    groupName = group.GroupName,
                                    groupCode = group.GroupCode,
                                    members = group.Members.Select(m => new { m.Id, m.Name }).ToList(),
                                    availability = group.Availability,
                                    votes = group.Votes
                                };
                            }
                            else
                            {
                                response.Data = new { success = false, message = "Group not found" };
                            }
                        }
                        else
                        {
                            response.Data = new { success = false, message = "Invalid data format" };
                        }
                        break;

                    case "GET_USER_GROUPS":
                        if (message.Data is JsonElement userGroupsData)
                        {
                            int userId = userGroupsData.GetProperty("userId").GetInt32();

                            using var db = new OrmManager();
                            var user = db.Users
                                .Include(u => u.Groups)
                                .FirstOrDefault(u => u.Id == userId);

                            if (user == null || user.Groups == null || !user.Groups.Any())
                            {
                                response.Data = new { success = false, message = "Sin grupos" };
                            }
                            else
                            {
                                response.Data = new
                                {
                                    success = true,
                                    groups = user.Groups.Select(g => new
                                    {
                                        g.Id,
                                        g.Name,
                                        g.InviteCode,
                                        g.CreateDate
                                    }).ToList()
                                };
                            }
                        }
                        else
                        {
                            response.Data = new { success = false, message = "Datos inválidos" };
                        }
                        break;


                    default:
                        response.Data = new { success = false, message = "Unknown command" };
                        break;
                }
            }
            catch (Exception ex)
            {
                response.Data = new { success = false, message = $"Error: {ex.Message}" };
            }

            return response;
        }

        static void ShowLocalIPs()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    Console.WriteLine($"   • {ip}");
                }
            }
        }
    }
}