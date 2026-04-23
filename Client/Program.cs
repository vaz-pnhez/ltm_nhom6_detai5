using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;



namespace ClientApplication
{
    class Program
    {
        static Socket client;
        // Biến lưu chuỗi hiện tại người dùng nhập
        static string currentInput = "";
        // Lock để đồng bộ khi truy cập currentInput
        static object lockInput = new object();

        // Lock để đồng bộ khi in ra console (tránh tình trạng tin nhắn mới đến làm loạn dòng đang gõ)
        static object consoleLock = new object();

        // Biến để đánh dấu khi đang trong quá trình đóng kết nối, tránh việc in nhiều thông báo lỗi khi Server ngắt kết nối
        static bool isShuttingdown = false;

        // Lock để đồng bộ khi thay đổi trạng thái đóng kết nối
        static object lockShutdown = new object();

        static void Main()
        {
            Console.Title = "Client Chat";
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Nhập ip Server để kết nối
            Console.Write("Nhập IP Server: ");
            string ipStr = Console.ReadLine();

            // Kiểm tra định dạng IP hợp lệ
            if (!IPAddress.TryParse(ipStr.Trim(), out IPAddress ip))
            {
                Console.WriteLine("Địa chỉ IP không hợp lệ. Vui lòng nhập lại.");
                return;
            }

            // Kết nối đến Server
            IPEndPoint endPoint = new IPEndPoint(ip, 5000);
            client.Connect(endPoint);
            Console.WriteLine("Đã kết nối Server!\n");

            // Gửi tên cho Server
            Console.Write("Nhập tên người dùng của bạn: ");
            string name = Console.ReadLine();
            client.Send(Encoding.UTF8.GetBytes(name));
            Console.Title = name;

            // Bắt đầu Thread nhận tin
            Thread receiveThread = new Thread(ReceiveMessage);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            // Bắt đầu gửi tin nhắn
            while (true)
            {

                // Nếu đang trong quá trình đóng kết nối thì sẽ thoát vòng gửi tin nhắn
                if (isShuttingdown)
                    break;

                Console.Write("> ");
                currentInput = "";

                while (true)
                {
                    // Nếu đang trong quá trình đóng kết nối thì sẽ thoát vòng gửi tin nhắn
                    if (isShuttingdown)
                        break;

                    // Đọc phím người dùng nhập vào, không hiển thị trên console (true)
                    var key = Console.ReadKey(true);

                    // Nếu nhấn Enter sẽ gửi tin đi
                    if (key.Key == ConsoleKey.Enter)
                    {
                        // Kiểm tra nếu người dùng nhập "/exit" thì sẽ thoát chương trình
                        if (currentInput.ToLower() == "/exit")
                        {
                            Console.WriteLine("\nĐang thoát...");

                            client.Close();
                            Environment.Exit(0);
                            break;
                        }

                        Console.WriteLine();

                        // Lưu giá trị currentInput vào một biến cục bộ để gửi đi, tránh xung đột với Thread nhận tin nhắn đang có thể cập nhật currentInput
                        string send_msg = currentInput;
                        try
                        {
                            // Gửi tin nhắn đi
                            client.Send(Encoding.UTF8.GetBytes(send_msg));
                            break;
                        }
                        catch
                        {
                            // Nếu có lỗi khi gửi tin (thường là do Server đã ngắt kết nối) thì sẽ gọi hàm đóng kết nối và thoát chương trình
                            if (!isShuttingdown)
                                ShutdownClient("\n[Không thể gửi tin nhắn! Server có thể đã ngắt kết nối!]");
                        }
                    }
                    // Nếu nhấn Backspace thì xoá đi 1 ký tự trong curentInput 
                    else if (key.Key == ConsoleKey.Backspace)
                    {
                        if (currentInput.Length > 0)
                        {
                            // Đồng bộ khi cập nhật currentInput để tránh xung đột với Thread nhận tin nhắn
                            lock (lockInput)
                                currentInput = currentInput.Substring(0, currentInput.Length - 1);

                            // Xoá ký tự cuối trên console (di chuyển con trỏ về trước, in một dấu cách để xoá, rồi lại di chuyển con trỏ về trước)
                            Console.Write("\b \b");
                        }
                    }
                    // Nếu nhấn ký tự bình thường thì thêm ký tự đó vào curentInput và in ra màn hình
                    else
                    {
                        // Đồng bộ khi cập nhật currentInput để tránh xung đột với Thread nhận tin nhắn
                        lock (lockInput)
                            currentInput += key.KeyChar;

                        // Đồng bộ khi in ra console để tránh xung đột với Thread nhận tin nhắn
                        lock (consoleLock)
                            Console.Write(key.KeyChar);
                    }
                }
            }

            // Hàm để đóng kết nối và thoát chương trình, có thể gọi khi phát hiện Server ngắt kết nối hoặc có lỗi nghiêm trọng
            static void ShutdownClient(string reason)
            {

                // Đồng bộ khi thay đổi trạng thái đóng kết nối để tránh việc in nhiều thông báo lỗi khi Server ngắt kết nối
                lock (lockShutdown)
                {
                    // Nếu đã đang trong quá trình đóng kết nối thì sẽ không làm gì nữa
                    if (isShuttingdown)
                        return;

                    isShuttingdown = true;
                }

                // Đồng bộ khi in ra console để tránh xung đột với Thread nhận tin nhắn
                lock (consoleLock)
                {
                    Console.WriteLine();
                    Console.WriteLine(reason);
                    Console.WriteLine("\nNhấn phím bất kì để thoát...");
                }

                // Đóng kết nối
                try { client.Shutdown(SocketShutdown.Both); } catch { }
                try { client.Close(); } catch { }

                // Đợi người dùng nhấn phím rồi thoát
                Console.ReadKey();
                Environment.Exit(0);
            }

            // Hàm Thread nhận tin nhắn
            static void ReceiveMessage()
            {
                byte[] buffer = new byte[1024];
                try
                {
                    while (true)
                    {

                        int received = client.Receive(buffer);

                        // Xử lý khi Server ngắt kết nối
                        if (received == 0)
                        {
                            ShutdownClient("\n[Server đã ngắt kết nối!]");
                            return;
                        }

                        // Nhận lấy tin nhắn từ Client
                        string rec_msg = Encoding.UTF8.GetString(buffer, 0, received);

                        // Tạm dừng thread
                        lock (consoleLock)
                        {
                            // Xoá dòng đang gõ dở để in tin nhắn mới đến
                            Console.Write("\r");
                            Console.Write(new string(' ', currentInput.Length + 2));
                            Console.Write("\r");

                            // In tin nhắn
                            Console.WriteLine(rec_msg);

                            // Viết lại dòng đang gõ dở
                            Console.Write("> " + currentInput);
                        }
                    }
                }

                // Xử lý khi Server ngắt kết nối đột ngột (ví dụ do lỗi mạng, Server bị crash...)
                catch (SocketException)
                {
                    ShutdownClient("\n[Server đã ngắt kết nối đột ngột!]");
                }

                // Xử lý lỗi khác
                catch (Exception ex)
                {
                    ShutdownClient("\n[Đã xảy ra lỗi: " + ex.Message + "]");
                }
            }
        }
    }
}