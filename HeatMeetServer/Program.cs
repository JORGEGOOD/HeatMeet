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
            Log.Add_Log("Cerrando ORM...");
            lock (ormLock)
            {
                ormManager?.Dispose();
            }
        }
        
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            ormManager.Database.EnsureCreated();

            //infinite client accept loop
            try
            {
                Log.Add_Log("=== HEATMEET SERVER ===");
                Socket serverSocket = NetUtils.NetUtils.CreateServerSocket("0.0.0.0", 8888);
                Log.Add_Log(new string('-', 60));

                while (serverSocket.IsBound)
                {
                    Socket clientSocket = serverSocket.Accept();
                    Thread clientThread = new Thread(HandleClient);
                    clientThread.Start(clientSocket);
                }
            }
            catch (Exception ex)
            {
                Log.Add_Log($"Error: {ex.Message}");
            }
        } 
    }
}
