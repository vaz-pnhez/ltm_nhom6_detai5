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
        // Tạo lock để đồng bộ khi truy cập clientNames và clients
        static object lockClients = new object();
        static object lockNames = new object();

        static void Main()
        {
            Console.Title = "Server Chat";
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            var ips = getAllUsableIPs();

            Console.WriteLine("Server đang chạy tại:");


            // Hiển thị tất cả các địa chỉ IP có thể sử dụng trên máy, phân loại theo loại mạng (Radmin, LAN, Other)
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
            skServer.Listen(100);

            Console.WriteLine("Server đang chờ Client...");
            while (true)
            {
                Socket skClient = skServer.Accept();

                // Thêm client vào danh sách, đồng thời khóa để tránh xung đột khi có nhiều client kết nối cùng lúc
                lock (lockClients)
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

            // Lấy tất cả các adapter mạng đang hoạt động
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var ni in interfaces)
            {
                // Bỏ qua các adapter ảo như VMware, VirtualBox
                string name = ni.Name.ToLower();
                string description = ni.Description.ToLower();

                // Nếu tên hoặc mô tả chứa "vmware" hoặc "virtual" thì bỏ qua
                if (name.Contains("vmware") || name.Contains("virtual") ||
                    description.Contains("vmware") || description.Contains("virtual"))
                    continue;

                // Lấy thông tin IP của adapter
                var ipProps = ni.GetIPProperties();

                // Lấy tất cả các địa chỉ IP unicast (IPv4) của adapter
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    // Chỉ lấy địa chỉ IPv4
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ip = addr.Address.ToString();

                        // Kiểm tra xem IP có phải là loopback hay không
                        if (ip.StartsWith("127.")) continue;

                        result.Add(ip);
                    }
                }
            }

            // Loại bỏ các địa chỉ IP trùng lặp (nếu có) và trả về danh sách
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

                // Lưu tên Client vào dictionary, đồng thời khóa để tránh xung đột khi có nhiều client kết nối cùng lúc
                lock (lockNames)
                    clientNames[client] = name;

                Console.WriteLine("\n> " + name + " đã tham gia\n");

                // Thông báo cho tất cả client khác biết có người mới tham gia
                Broadcast(name + " đã tham gia phòng chat", client);

                // Vòng lặp nhận tin nhắn từ client
                while (true)
                {
                    // Nhận tin nhắn từ client
                    recv = client.Receive(buffer);
                    if (recv == 0) break;

                    // Chuyển đổi tin nhắn từ byte[] sang string
                    string msg = Encoding.UTF8.GetString(buffer, 0, recv);
                    Console.WriteLine(name + ": " + msg);

                    Broadcast(name + ": " + msg, client);
                }
            }

            // Nếu có lỗi khi nhận dữ liệu, có thể do client đã ngắt kết nối
            catch (Exception e) 
            {
                Console.WriteLine("Mất kết nối với " + client.RemoteEndPoint);
            }

            // Khi client thoát
            if (clientNames.ContainsKey(client))
            {
                string name = clientNames[client];
                Console.WriteLine("\n< " + name + " đã rời\n");

                Broadcast(name + " đã rời phòng chat", client);

                clientNames.Remove(client);
            }

            // Xóa client khỏi danh sách, đồng thời khóa để tránh xung đột khi có nhiều client kết nối cùng lúc
            lock (lockClients)
                clients.Remove(client);

            client.Close();
        }


        // Hàm gửi tin nhắn đến tất cả client, ngoại trừ client đã gửi tin nhắn. Nếu có lỗi khi gửi dữ liệu, có thể do client đã ngắt kết nối, sẽ xóa client đó khỏi danh sách
        static void Broadcast(string message, Socket sender)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);

            // Tạo một danh sách tạm để lưu các client cần xóa do lỗi khi gửi dữ liệu
            List<Socket> toRemove = new List<Socket>();

            foreach (Socket client in clients)
            {
                // Không gửi lại cho client đã gửi tin nhắn
                if (client != sender)
                {
                    try
                    {
                        client.Send(data);
                    }
                    catch (Exception e)
                    {
                        // Nếu có lỗi khi gửi dữ liệu, có thể do client đã ngắt kết nối. Thêm client vào danh sách cần xóa
                        Console.WriteLine("Lỗi khi gửi đến " + client.RemoteEndPoint + ": " + e.Message);
                        toRemove.Add(client);
                    }
                }
            }

            // Xóa các client không thể gửi dữ liệu
            lock (lockClients)
            {
                foreach (var client in toRemove)
                {
                    clients.Remove(client);
                    client.Close();
                }
            }
        }
    }
}