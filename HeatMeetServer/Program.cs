using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SharedModels;
using NetUtils;

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


            // Test messages
            if (currentGroup != null)
            {
                bool hasMessages = ormManager.Messages.Any(m => m.GroupId == currentGroup.Id);

                if (!hasMessages)
                {
                    if (userJorge != null && userAdmin != null)
                    {
                        var messages = new List<Messages>
            {
                new Messages
                {
                    Content = "¿Qué tal quedamos esta tarde?",
                    CreateDate = DateTime.UtcNow.AddMinutes(-30),
                    UserId = userJorge.Id,
                    GroupId = currentGroup.Id
                },
                new Messages
                {
                    Content = "Genial, ¿a qué hora?",
                    CreateDate = DateTime.UtcNow.AddMinutes(-25),
                    UserId = userAdmin.Id,
                    GroupId = currentGroup.Id
                },
                new Messages
                {
                    Content = "¿Os parece sobre las 18:00?",
                    CreateDate = DateTime.UtcNow.AddMinutes(-20),
                    UserId = userJorge.Id,
                    GroupId = currentGroup.Id
                },
                new Messages
                {
                    Content = "Perfecto, allí nos vemos",
                    CreateDate = DateTime.UtcNow.AddMinutes(-15),
                    UserId = userAdmin.Id,
                    GroupId = currentGroup.Id
                }
            };

                        ormManager.Messages.AddRange(messages);
                        ormManager.SaveChanges();

                        Console.WriteLine("Test messages added.");
                    }
                }
                else
                {
                    Console.WriteLine("Test messages already exist.");
                }
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

                while (serverSocket.IsBound)
                {
                    Socket clientSocket = serverSocket.Accept();
                    Console.WriteLine($"Cliente connect");
                    Thread clientThread = new Thread(HandleClient);
                    clientThread.Start(clientSocket);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }


           
        }


        static void HandleClient(object? obj)
        {
            if (obj is not Socket client) return;
            try
            {
                while (true)
                {
                    var message = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(client);
                    if (message == null) break;

                    Console.WriteLine($" Comando recibido: {message.Command}");
                    NetworkMessage response = ProcessCommand(message);

                    NetUtils.NetUtils.SendJson(client, response);
                    Console.WriteLine($" Respuesta enviada");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Error cliente: {ex.Message}");
            }
            finally
            {
                NetUtils.NetUtils.CloseSocket(client);
                Console.WriteLine(" Cliente desconectado");
            }
        }

        static NetworkMessage ProcessCommand(NetworkMessage message)
        {
            NetworkMessage response = new NetworkMessage { Command = message.Command };

            try
            {
                switch (message.Command)
                {
                    case "LOGIN":
                        if (message.Data is JsonElement loginData)
                        {
                            string email = loginData.GetProperty("email").GetString() ?? "";
                            string password = loginData.GetProperty("password").GetString() ?? "";

                            using var db = new OrmManager();
                            var user = db.Users.FirstOrDefault(u => u.Email == email || u.Name == email);

                            if (user == null)
                                response.Data = new { success = false, message = "User doesn't exists", userId = 0, userName = "" };
                            else if (user.Password != password)
                                response.Data = new { success = false, message = "Incorrect password", userId = 0, userName = "" };
                            else
                                response.Data = new { success = true, message = "Login correct", userId = user.Id, userName = user.Name };
                        }
                        else
                        {
                            response.Data = new { success = false, message = "Invalid data", userId = 0, userName = "" };
                        }
                        break;

                    case "REGISTER":
                        if (message.Data is JsonElement registerData)
                        {
                            string name = registerData.GetProperty("name").GetString() ?? "";
                            string email = registerData.GetProperty("email").GetString() ?? "";
                            string password = registerData.GetProperty("password").GetString() ?? "";

                            using var db = new OrmManager();
                            var exists = db.Users.FirstOrDefault(u => u.Email == email);

                            if (exists != null)
                                response.Data = new { success = false, message = "Email already registered" };
                            else
                            {
                                db.Users.Add(new Users { Name = name, Email = email, Password = password });
                                db.SaveChanges();
                                response.Data = new { success = true, message = "User registered correctly" };
                            }
                        }
                        else
                        {
                            response.Data = new { success = false, message = "Invalid data" };
                        }
                        break;

                    case "CREATE_GROUP":
                        if (message.Data is JsonElement createGroupData)
                        {
                            string groupName = createGroupData.GetProperty("groupName").GetString() ?? "";
                            int adminId = createGroupData.GetProperty("userId").GetInt32();

                            using var db = new OrmManager();
                            var user = db.Users.FirstOrDefault(u => u.Id == adminId);

                            if (user == null)
                                response.Data = new { success = false, message = "User not found", inviteCode = "" };
                            else
                            {
                                var newGroup = new Groups
                                {
                                    Name = groupName,
                                    InviteCode = Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
                                    CreateDate = DateTime.UtcNow
                                };
                                newGroup.Users.Add(user);
                                db.Groups.Add(newGroup);
                                db.SaveChanges();
                                response.Data = new { success = true, message = "Group created", inviteCode = newGroup.InviteCode };
                            }
                        }
                        else
                        {
                            response.Data = new { success = false, message = "Invalid data", inviteCode = "" };
                        }
                        break;

                    case "JOIN_GROUP":
                        if (message.Data is JsonElement joinGroupData)
                        {
                            string inviteCode = joinGroupData.GetProperty("inviteCode").GetString() ?? "";
                            int userId = joinGroupData.GetProperty("userId").GetInt32();

                            using var db = new OrmManager();
                            var group = db.Groups.Include(g => g.Users).FirstOrDefault(g => g.InviteCode == inviteCode);
                            var user = db.Users.FirstOrDefault(u => u.Id == userId);

                            if (group == null)
                                response.Data = new { success = false, message = "Group not found" };
                            else if (user == null)
                                response.Data = new { success = false, message = "User not found" };
                            else if (group.Users.Any(u => u.Id == userId))
                                response.Data = new { success = false, message = "Already in this group" };
                            else
                            {
                                group.Users.Add(user);
                                db.SaveChanges();
                                response.Data = new { success = true, message = "Joined group correctly" };
                            }
                        }
                        else
                        {
                            response.Data = new { success = false, message = "Invalid data" };
                        }
                        break;


                    case "GET_USER_GROUPS":
                        if (message.Data is JsonElement userGroupsData)
                        {
                            int userId = userGroupsData.GetProperty("userId").GetInt32();

                           
                            var user = ormManager.Users
                                .Include(u => u.Groups)
                                .FirstOrDefault(u => u.Id == userId);

                            if (user == null || user.Groups == null || !user.Groups.Any())
                                response.Data = new { success = false, message = "No groups found" };
                            else
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
                        else
                        {
                            response.Data = new { success = false, message = "Invalid data" };
                        }
                        break;

                    default:
                        response.Data = new { success = false, message = "Unknown command" };
                        break;
                }
            }
            catch (Exception ex)
            {
                response.Data = new { success = false, message = "Error: "+ ex.Message};
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