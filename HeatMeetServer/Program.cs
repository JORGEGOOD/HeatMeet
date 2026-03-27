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

    public partial class Program
    {
        public static readonly object ormLock = new object();
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
    }
}
