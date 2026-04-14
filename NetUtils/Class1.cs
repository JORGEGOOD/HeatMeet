using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;

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

        public static Socket ConnectToServer()
        {
            Socket socket = CreateClientSocket("192.168.111.28", 8888);
            return socket;
        }

        public static void SendJson(Socket s, object data)
        {
            s.SendTimeout = 10000;
            //custom option to avoid possible infinite sending loop
            JsonSerializerOptions options = new JsonSerializerOptions 
            { 
                ReferenceHandler = ReferenceHandler.IgnoreCycles,//<-- Ignore possible Orm infinites
                WriteIndented = false//<-- Ignore pretty printing, it should already be by default tho
            };

            string json = JsonSerializer.Serialize(data,options);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            byte[] lengthBytes = BitConverter.GetBytes(jsonBytes.Length);

            s.Send(lengthBytes);  //length first
            //send json by pieces
            int sent = 0;
            while(sent<jsonBytes.Length)
            {
                int r = s.Send(jsonBytes, sent, jsonBytes.Length - sent, SocketFlags.None);
                if (r == 0) throw new Exception("Could not sent data");
                sent += r;
            }
        }

        public static T? ReceiveJson<T>(Socket s)
        {
            s.ReceiveTimeout = 10000;
            //Get total lenght
            byte[] lengthBuffer = new byte[4];
            int received = 0;
            while (received < 4)
            {
                int r = s.Receive(lengthBuffer, received, 4 - received, SocketFlags.None);
                if (r == 0) throw new Exception("Socket closed while reading length");
                received += r;
            }
            int length = BitConverter.ToInt32(lengthBuffer, 0);

            //Get Json on multiple parts
            byte[] buffer = new byte[length];
            received = 0;
            while (received < length)
            {
                int r = s.Receive(buffer, received, length - received, SocketFlags.None);
                if (r == 0) throw new Exception("Socket closed while reading data");
                received += r;
            }
            string json = Encoding.UTF8.GetString(buffer);

            return JsonSerializer.Deserialize<T>(json);
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
