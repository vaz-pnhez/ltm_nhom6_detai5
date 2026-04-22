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
            Console.Title = "Server Chat";
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            var ips = getAllUsableIPs();

            Console.WriteLine("Server đang chạy tại:");

            foreach (var ip in ips) 
            {
                if (ip.StartsWith("26.")) 
                    Console.WriteLine("[Radmin] " + ip + ":5000");
                else if (ip.StartsWith("192.168.") || ip.StartsWith("10.") || ip.StartsWith("172."))
                    Console.WriteLine("[LAN] " + ip + ":5000");
                else
                    Console.WriteLine("[Other] " + ip + ":5000");
            }


            //    string ip = Dns.GetHostEntry(Dns.GetHostName())
            //        .AddressList
            //        .First(x => x.AddressFamily == AddressFamily.InterNetwork
            //         && !IPAddress.IsLoopback(x))
            //.ToString();

            //        Console.WriteLine("Server đang chạy tại: " + ip + ":5000");

            // Khai bao dia chi ip
            IPAddress ipadd = IPAddress.Any;
            // tao mot end point
            IPEndPoint ipend = new IPEndPoint(ipadd, 5000);

            Socket skServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // thuc hien bin ipendpoint socket va lang nghe
            skServer.Bind(ipend);
            skServer.Listen(10);

            Console.WriteLine("Server đang chờ Client...");
            while (true)
            {
                Socket skClient = skServer.Accept();
                clients.Add(skClient);

                Console.WriteLine("Chấp nhận kết nối từ " + skClient.RemoteEndPoint);

                Thread t = new Thread(HandleThread);
                t.IsBackground = true;
                t.Start(skClient);

            }
        }

        // Hàm lấy ra tất cả các địa chỉ IP có thể sử dụng trên máy, loại bỏ loopback và adapter ảo
        public static List<string> getAllUsableIPs()
        {
            var result = new List<string>();

            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var ni in interfaces)
            {
                // Bỏ qua các adapter ảo như VMware, VirtualBox
                string name = ni.Name.ToLower();
                string description = ni.Description.ToLower();

                if (name.Contains("vmware") || name.Contains("virtual") ||
                    description.Contains("vmware") || description.Contains("virtual"))
                    continue;

                var ipProps = ni.GetIPProperties();

                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ip = addr.Address.ToString();

                        // Kiểm tra xem IP có phải là loopback hay không
                        if (ip.StartsWith("127.")) continue;

                        result.Add(ip);
                    }
                }
            }

            return result.Distinct().ToList();
        }

        static void HandleThread(object obj)
        {
            Socket client = (Socket)obj;
            byte[] buffer = new byte[1024];

            try
            {

                // Nhận tên Client và thông báo tham gia
                int recv = client.Receive(buffer);
                string name = Encoding.UTF8.GetString(buffer, 0, recv);

                clientNames[client] = name;

                Console.WriteLine("\n> " + name + " đã tham gia\n");

                Broadcast(name + " đã tham gia phòng chat", client);
                while (true)
                {
                    recv = client.Receive(buffer);
                    if (recv == 0) break;

                    string msg = Encoding.UTF8.GetString(buffer, 0, recv);
                    Console.WriteLine(name + ": " + msg);

                    Broadcast(name + ": " + msg, client);
                }
            }
            catch { }

            // Khi client thoát
            if (clientNames.ContainsKey(client))
            {
                string name = clientNames[client];
                Console.WriteLine("\n< " + name + " đã rời\n");

                Broadcast(name + " đã rời phòng chat", client);

                clientNames.Remove(client);
            }

            clients.Remove(client);
            client.Close();
        }

        static void Broadcast(string message, Socket sender)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);

            foreach (Socket client in clients)
            {
                if (client != sender)
                {
                    try
                    {
                        client.Send(data);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Lỗi khi gửi đến " + client.RemoteEndPoint + ": " + e.Message);
                        break;
                    }
                }
            }
        }
    }
}