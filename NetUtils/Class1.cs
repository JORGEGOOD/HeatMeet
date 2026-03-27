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


        public static Socket ConnectToServer()
        {
            Socket socket = CreateClientSocket("10.0.2.2", 8888);
            return socket;
        }



        public static void SendJson(Socket s, object data)
        {
            string json = JsonSerializer.Serialize(data);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

            byte[] lengthBytes = BitConverter.GetBytes(jsonBytes.Length);

            s.Send(lengthBytes);  // primero tamaño
            s.Send(jsonBytes);    // luego datos
        }

        public static T? ReceiveJson<T>(Socket s)
        {
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
