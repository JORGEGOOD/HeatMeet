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

        #region Sockets

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
            Socket socket = CreateClientSocket("192.168.111.41", 8888);
            return socket;
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
            if (s.Connected) s.Shutdown(SocketShutdown.Both);

            s.Close();
        }

        #endregion

        #region Json

        // --- JSON SEND WITH ACK ---
        public static void SendJson(Socket socket, object data)
        {
            if (socket == null) return;
            socket.SendTimeout = 10000;

            JsonSerializerOptions JsonOptions = new() //Ignore pretty printing (spaces and intros) and dodge possible Orm infinites
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                WriteIndented = false
            };

            string json = JsonSerializer.Serialize(data, JsonOptions);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            byte[] lengthBytes = BitConverter.GetBytes(jsonBytes.Length);

            //Send length
            socket.Send(lengthBytes);

            //Wait for ACK
            //This is just a useless patch for a bigger problem
            byte[] ack = new byte[1];
            int ackReceived = socket.Receive(ack);
            if (ackReceived <= 0) throw new Exception("Connection lost, waiting ACK");

            //Send JSON in parts
            int sent = 0;
            while (sent < jsonBytes.Length)
            {
                int r = socket.Send(jsonBytes, sent, jsonBytes.Length - sent, SocketFlags.None);
                if (r == 0) throw new Exception("Error when sending data (Sent 0 bytes)");
                sent += r;
            }
        }

        // --- JSON RECEIVE WITH ACK ---
        public static T? ReceiveJson<T>(Socket socket)
        {
            if (socket == null) return default;
            socket.ReceiveTimeout = 10000;

            //Get length, should be in int 
            byte[] lengthBuffer = new byte[4];
            int receivedLength = 0;
            while (receivedLength < 4)
            {
                int r = socket.Receive(lengthBuffer, receivedLength, 4 - receivedLength, SocketFlags.None);
                if (r <= 0) return default;
                receivedLength += r;
            }
            int length = BitConverter.ToInt32(lengthBuffer, 0);

            //Send  ACK 
            socket.Send(new byte[] { 1 });

            //Get JSON in parts
            byte[] buffer = new byte[length];
            int receivedBody = 0;
            while (receivedBody < length)
            {
                int r = socket.Receive(buffer, receivedBody, length - receivedBody, SocketFlags.None);
                if (r <= 0) break;
                receivedBody += r;
            }

            if (receivedBody < length) return default;

            string json = Encoding.UTF8.GetString(buffer);
            return JsonSerializer.Deserialize<T>(json);
        }

        #endregion

        #region Binary_not_implemented

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

        #endregion

    }
}
