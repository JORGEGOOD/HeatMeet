using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Text;

namespace NetUtils
{
    public static class NetUtils
    {
        public static Socket CreateServerSocket(string addressText, int port)
        {
            IPAddress address = IPAddress.Parse(addressText);
            IPEndPoint endpoint = new IPEndPoint(address, port);

            Socket serverSocket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            serverSocket.Bind(endpoint);
            serverSocket.Listen();

            return serverSocket;
        }
        public static string AutodetectIpAddress()
        {
            string hostName = Dns.GetHostName();
            IPHostEntry host = Dns.GetHostEntry(hostName);
            int index = host.AddressList.ToList().FindIndex((e) => e.AddressFamily == AddressFamily.InterNetwork);

            return host.AddressList[index].ToString();

        }
        public static void SendJson(Socket s, object data)
        {
            string json = JsonSerializer.Serialize(data);
            byte[] line = Encoding.UTF8.GetBytes(json);
            s.Send(line);
        }

        public static T ReceiveJson<T>(Socket s)
        {
            byte[] buffer = new byte[4096];
            int len = s.Receive(buffer);

            string json = Encoding.UTF8.GetString(buffer, 0, len);
            return JsonSerializer.Deserialize<T>(json)!;
        }

        public static Socket CreateClientSocket(string addressText, int port)
        {
            IPAddress address = IPAddress.Parse(addressText);
            IPEndPoint endpoint = new IPEndPoint(address, port);

            Socket clientSocket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Connect(endpoint);

            return clientSocket;
        }

        public static void CloseSocket(Socket s)
        {
            if (s.Connected)
                s.Shutdown(SocketShutdown.Both);

            s.Close();
        }
    }
}
