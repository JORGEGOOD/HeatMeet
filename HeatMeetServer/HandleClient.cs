using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SharedModels;

namespace HeatMeetServer
{
    public partial class Program
    {
        static void HandleClient(object? obj)
        {
            if (obj is not Socket client){ Console.WriteLine("FATAL ERROR: HandleClient didn't received a Socket"); return; }
            try
            {
                //every "client" is just an individual command
                NetworkMessage message = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(client);

                if (message == null) return;
                
                Console.WriteLine($" Command received: {message.Command}");
                
                NetworkMessage response = ProcessCommand(message);

                NetUtils.NetUtils.SendJson(client, response);

            }
            catch (Exception ex)
            {
                Console.WriteLine($" Error client: {ex.Message}");
            }
            finally
            {
                NetUtils.NetUtils.CloseSocket(client);
            }
        }
    }
}
