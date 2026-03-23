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
        
        public static OrmManager ormManager {  get; private set; } = new OrmManager();

        private static void OnProcessExit(object? sender, EventArgs e)//to dispose the app on exit
        {
            Console.WriteLine("Cerrando ORM...");
            ormManager?.Dispose();
        }
        
        static void Main(string[] args)
        {

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            
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
                Socket serverSocket = NetUtils.NetUtils.CreateServerSocket("0.0.0.0", 8888);
                Console.WriteLine($"Server initiated in 0.0.0.0:8888 (all ip's)");
                Console.WriteLine(new string('-', 60));

                while (serverSocket.IsBound)
                {
                    Socket clientSocket = serverSocket.Accept();
                    Console.WriteLine($"Client connected!");
                    Thread clientThread = new Thread(HandleClient);
                    clientThread.Start(clientSocket);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void HandleClient(object? obj)
        {
            if (obj is not Socket client) return;
            try
            {
                // while (true) //Sergio: This should NOT be a loop, each button is a new connection
                //{
                var message = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(client);
                
                if (message == null) return;

                Console.WriteLine($" Command received: {message.Command}");
                NetworkMessage response = ProcessCommand(message);

                NetUtils.NetUtils.SendJson(client, response);
                Console.WriteLine($" Answer sent");
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Error client: {ex.Message}");
            }
            finally
            {
                NetUtils.NetUtils.CloseSocket(client);
                Console.WriteLine(" Client disconnected");
            }
        }

        static NetworkMessage ProcessCommand(NetworkMessage message)
        {
            NetworkMessage response = new NetworkMessage { Command = message.Command };
            try
            {
                switch (message.Command)
                {
                    case "ACK":
                        //this means "Acknowleded" or Sucess, here should be nothing or a Log message.
                        Console.WriteLine("Anser received: ACK");
                    break;

                    case "LOGIN":
                        if (message.Data is JsonElement loginData)
                        {
                            string email = loginData.GetProperty("email").GetString() ?? "";
                            string password = loginData.GetProperty("password").GetString() ?? "";
                            
                            var user = ormManager.Users.FirstOrDefault(u => u.Email == email || u.Name == email);

                            if (user == null)
                                response.Data = new { success = false, message = "User doesn't exists", userId = 0, userName = "" };
                            else if (user.Password != password)
                                response.Data = new { success = false, message = "Incorrect password", userId = 0, userName = "" };
                            else
                                response.Data = new { success = true,  message = "Login correct", userId = user.Id, userName = user.Name };
                        }
                        else response.Data = new { success = false, message = "Invalid data", userId = 0, userName = "" };
                    break;

                    case "REGISTER":
                        if (message.Data is JsonElement registerData)
                        {
                            string name     = registerData.GetProperty("name").GetString()     ?? "";
                            string email    = registerData.GetProperty("email").GetString()    ?? "";
                            string password = registerData.GetProperty("password").GetString() ?? "";

                            var exists = ormManager.Users.FirstOrDefault(u => u.Email == email);

                            if (exists != null)
                                response.Data = new { success = false, message = "Email already registered" };
                            else
                            {
                                ormManager.Users.Add(new Users { Name = name, Email = email, Password = password });
                                ormManager.SaveChanges();
                                response.Data = new { success = true, message = "User registered correctly" };
                            }
                        }
                        else response.Data = new { success = false, message = "Invalid data" };
                        
                        break;

                    case "CREATE_GROUP":
                        if (message.Data is JsonElement createGroupData)
                        {
                            string groupName = createGroupData.GetProperty("groupName").GetString() ?? "";
                            int adminId = createGroupData.GetProperty("userId").GetInt32();

                            using var ormManager = new OrmManager();
                            var user = ormManager.Users.FirstOrDefault(u => u.Id == adminId);

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
                                ormManager.Groups.Add(newGroup);
                                ormManager.SaveChanges();
                                response.Data = new { success = true, message = "Group created", inviteCode = newGroup.InviteCode };
                            }
                        }
                        else response.Data = new { success = false, message = "Invalid data", inviteCode = "" };
                        break;
                    case "JOIN_GROUP":
                        if (message.Data is JsonElement joinGroupData)
                        {
                            string inviteCode = joinGroupData.GetProperty("inviteCode").GetString() ?? "";
                            int userId = joinGroupData.GetProperty("userId").GetInt32();

                            
                            var group = ormManager.Groups.Include(g => g.Users).FirstOrDefault(g => g.InviteCode == inviteCode);
                            var user = ormManager.Users.FirstOrDefault(u => u.Id == userId);

                            if (group == null)
                                response.Data = new { success = false, message = "Group not found" };
                            else if (user == null)
                                response.Data = new { success = false, message = "User not found" };
                            else if (group.Users.Any(u => u.Id == userId))
                                response.Data = new { success = false, message = "Already in this group" };
                            else
                            {
                                group.Users.Add(user);
                                ormManager.SaveChanges();
                                response.Data = new { success = true, message = "Joined group correctly" };
                            }
                        }
                        else response.Data = new { success = false, message = "Invalid data" };
                        
                        break;
                    case "GET_USER_GROUPS":
                        if (message.Data is JsonElement userGroupsData)
                        {
                            int userId = userGroupsData.GetProperty("userId").GetInt32();
                            Users? user = ormManager.Users
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
                        else response.Data = new { success = false, message = "Invalid data" };
                        
                        break;
                    case "GET_GROUP_MESSAGES":

                        if (message.Data is JsonElement groupMessages)
                        {
                            int groupId = groupMessages.GetProperty("groupId").GetInt32();

                            //now we do select to the database and retreat the messages, put in into a json and send back
                            var messages = ormManager.Messages.Where(m => m.GroupId == groupId).Select(m => new { m.Content,m.CreateDate,m.UserId,UserName = m.User.Name}).ToList();
                            if( messages == null || messages.Count == 0) response.Data = new { success = false, messages = "No messages found" };
                            else
                            {
                                //send all messages
                                response.Data = new
                                {
                                    success = true,
                                    messages = messages
                                };
                            }
                        }
                        else response.Data = new { success = false, message = "Invalid data" };
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

        
    }
}