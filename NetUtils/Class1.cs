using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace NetUtils
{
    public class netUtils
    {
        // 
        public void SendJson(Socket s, string data)
        {
            string json = JsonSerializer.Serialize(data);
            byte[] line = Encoding.UTF8.GetBytes(json + "\n");
            s.Send(line);
        }
        // 
        public T ReceiveJson<T>(Socket s)
        {
            byte[] buffer = new byte[2048];
            int len = s.Receive(buffer);

            return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(buffer));
        }

        public void ConnectToServer()
        {
            Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            client.Connect("localhost", 5555);
            Console.WriteLine("Conectado al servidor!");
        }

        public static Socket CreateClientSocket(string addressText, int port)
        {
            IPAddress address = IPAddress.Parse(addressText);
            IPEndPoint endpoint = new IPEndPoint(address, port);

            Socket clientSocket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            clientSocket.Connect(endpoint);

            return clientSocket;
        }
        public static string AutodetectIpAddress()
        {
            string hostName = Dns.GetHostName();
            IPHostEntry host = Dns.GetHostEntry(hostName);
            int index = host.AddressList.ToList().FindIndex((e) => e.AddressFamily == AddressFamily.InterNetwork);

            return host.AddressList[index].ToString();

        }
    }
}



