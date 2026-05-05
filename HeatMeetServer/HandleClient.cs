using System.Net.Sockets;
using SharedModels;

namespace HeatMeetServer
{
    public partial class Program
    {
        static void HandleClient(object? obj)
        {
            if (obj is not Socket client){ Log.Add_Log("FATAL ERROR: HandleClient didn't received a Socket"); return; }
            try
            {
                //every client is in reality an individual command
                NetworkMessage? message = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(client);

                if (message == null) return;

                bool isReload = message.Command == "RELOAD_CHAT_MESSAGES";//All of theese conditionals are for preventing "RELOAD" messages invading the log

                if (!isReload)
                {
                    Log.Add_Log($"- New connection: {client.RemoteEndPoint} -");
                    Log.Add_Log($"{message.Command}");
                }

                NetworkMessage response = ProcessCommand(message);

                NetUtils.NetUtils.SendJson(client, response);

                if (!isReload)
                {
                    Log.Add_Log($"--------------------------------------");
                }
            }
            catch (Exception ex)
            {
                Log.Add_Log($" Error client: {ex.Message}");
            }
            finally
            {
                NetUtils.NetUtils.CloseSocket(client);
            }
        }
    }
}
