using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Linq;

namespace ServerApplication
{
    public class Program
    {
        // Tạo dictionary để lưu tên giá 
        static Dictionary<Socket, string> clientNames = new Dictionary<Socket, string>();
        // Tạo List Client
        static List<Socket> clients = new List<Socket>();
        static void Main()
        {
            string ip = Dns.GetHostEntry(Dns.GetHostName())
            .AddressList
            .First(x => x.AddressFamily == AddressFamily.InterNetwork
             && !IPAddress.IsLoopback(x))
    .ToString();

            Console.WriteLine("Server dang chay tai: " + ip + ":5000");

            // Khai bao dia chi ip
            IPAddress ipadd = IPAddress.Any;
            // tao mot end point
            IPEndPoint ipend = new IPEndPoint(ipadd, 5000);

            Socket skServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // thuc hien bin ipendpoint socket va lang nghe
            skServer.Bind(ipend);
            skServer.Listen(10);

            Console.WriteLine("Server dang cho Client...");
            while (true)
            {
                Socket skClient = skServer.Accept();
                clients.Add(skClient);

                Console.WriteLine("Chap nhan ket noi tu " + skClient.RemoteEndPoint);

                Thread t = new Thread(HandleThread);
                t.IsBackground = true;
                t.Start(skClient);

            }
        }
        static void HandleThread(object obj)
        {
            Socket client = (Socket)obj;
            byte[] buffer = new byte[1024];

            try
            {

                // Nhận tên Client và thông báo tham gia
                int recv = client.Receive(buffer);
                string name = Encoding.Unicode.GetString(buffer, 0, recv);

                clientNames[client] = name;

                Console.WriteLine(name + " da tham gia");

                Broadcast(name + " da tham gia phong chat", client);
                while (true)
                {
                    recv = client.Receive(buffer);
                    if (recv == 0) break;

                    string msg = Encoding.Unicode.GetString(buffer, 0, recv);
                    Console.WriteLine(name + ": " +msg);

                    Broadcast(name + ": " + msg, client);
                }
            }
            catch { }

            // Khi client thoát
            if (clientNames.ContainsKey(client))
            {
                string name = clientNames[client];
                Console.WriteLine(name + " da roi");

                Broadcast(name + " da roi phong chat", client);

                clientNames.Remove(client);
            }

            clients.Remove(client);
            client.Close();
        }

        static void Broadcast(string message, Socket sender)
        {
            byte[] data = Encoding.Unicode.GetBytes(message);

            foreach (Socket client in clients)
            {
                if (client != sender)
                {
                    try
                    {
                        client.Send(data);
                    }
                    catch { }
                }
            }
        }
    }
}