using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;
using System;

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

        public static Socket ConnectToServer()
        {
            Socket socket = CreateClientSocket("192.168.111.40", 8888);
            return socket;
        }

        public static void SendJson(Socket socket, object data)
        {
            socket.SendTimeout = 10000;

            JsonSerializerOptions options = new()
            { 
                ReferenceHandler = ReferenceHandler.IgnoreCycles,//<-- Ignore theoretically possible Orm infinites
                WriteIndented = false//<-- Ignore pretty printing. Faster, should be default tho
            };

            string json = JsonSerializer.Serialize(data,options);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            byte[] lengthBytes = BitConverter.GetBytes(jsonBytes.Length);

            socket.Send(lengthBytes);//Length first
            //send json by pieces
            int sent = 0;
            while(sent<jsonBytes.Length)
            {
                int r = socket.Send(jsonBytes, sent, jsonBytes.Length - sent, SocketFlags.None);
                if (r == 0) throw new Exception("Could not sent data");
                sent += r;
            }
        }

        public static T? ReceiveJson<T>(Socket socket)//HUGE functions to check Network errors, obviously they failed
        {
            socket.ReceiveTimeout = 10000;

            //Receive length
            byte[] lengthBuffer = new byte[4];
            int received = 0;
            while (received < 4)
            {
                int r = socket.Receive(lengthBuffer, received, 4 - received, SocketFlags.None);
                if (r <= 0) return default; 
                received += r;
            }
            int length = BitConverter.ToInt32(lengthBuffer, 0);
            
            //Receive Json
            byte[] buffer = new byte[length];
            received = 0;
            while (received < length)
            {
                int r = socket.Receive(buffer, received, length - received, SocketFlags.None);
                if (r <= 0) break; 
                received += r;
            }
            
            if (received < length) return default;

            string json = Encoding.UTF8.GetString(buffer);
            return JsonSerializer.Deserialize<T>(json);
        }

        //-- Binary for images and large files --
        public static void SendBinary(Socket socket, byte[] data)//<-- C# manages pointers by default
        {
            socket.ReceiveTimeout = 10000;

            int totalSent = 0;
            int size      = data.Length;

            while(totalSent < size)
            {
                int sent = socket.Send(data, totalSent, size - totalSent, SocketFlags.None);
                if(sent==0) throw new SocketException((int)SocketError.ConnectionAborted);
                totalSent += sent;
            }
        }

        public static void ReceiveBinary(Socket socket, int fileSize, Stream destination)
        {
            byte[] bytes = new byte[8192];
            int totalRead = 0;
            while (totalRead < fileSize)
            {
                int toRead   = Math.Min(bytes.Length, fileSize - totalRead);
                int received = socket.Receive(bytes, 0, toRead, SocketFlags.None);

                if (received == 0) throw new SocketException((int)SocketError.ConnectionAborted);

                destination.Write(bytes, 0, received);
                totalRead += received;
            }
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
