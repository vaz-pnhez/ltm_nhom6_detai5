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
        static object consoleLock = new object();

        static void Main()
        {
            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Nhập ip Server để kết nối
            Console.Write("Nhap IP server: ");
            string ipStr = Console.ReadLine();
            IPAddress ip = IPAddress.Parse(ipStr);
            IPEndPoint endPoint = new IPEndPoint(ip, 5000);
            client.Connect(endPoint);
            Console.WriteLine("Da ket noi server!");

            // Gửi tên cho Server
            Console.Write("Nhap ten cua ban: ");
            string name = Console.ReadLine();
            client.Send(Encoding.Unicode.GetBytes(name));

            // Bắt đầu Thread nhận tin
            Thread receiveThread = new Thread(ReceiveMessage);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            // Bắt đầu gửi tin nhắn
            while (true)
            {
                Console.Write("> ");
                currentInput = "";

                while (true)
                {
                    // Cho phép nhập liệu 1 ký tự
                    var key = Console.ReadKey(true);

                    // Nếu nhấn Enter sẽ gửi tin đi
                    if (key.Key == ConsoleKey.Enter)
                    {
                        Console.WriteLine();

                        string name_msg = currentInput;
                        // Gửi đi tên và message cho Server
                        client.Send(Encoding.Unicode.GetBytes(name_msg));
                        break;
                    }
                    // Nếu nhấn Backspace thì xoá đi 1 ký tự trong curentInput 
                    else if (key.Key == ConsoleKey.Backspace)
                    {
                        if (currentInput.Length > 0)
                        {
                            currentInput = currentInput.Substring(0, currentInput.Length - 1);
                            Console.Write("\b \b");
                        }
                    }
                    // Nếu nhấn ký tự bình thường thì thêm ký tự đó vào curentInput và in ra màn hình
                    else
                    {
                        currentInput += key.KeyChar;
                        Console.Write(key.KeyChar);
                    }
                }
            }
        }

        // Hàm Thread nhận tin nhắn
        static void ReceiveMessage()
        {
            byte[] buffer = new byte[1024];
            while (true)
            {
                try
                {
                    int received = client.Receive(buffer);
                    if (received == 0) break;

                    // Nhận lấy tin nhắn từ Client
                    string msg = Encoding.Unicode.GetString(buffer, 0, received);

                    // Tmj dừng thread
                    lock (consoleLock)
                    {
                        // Xoá dòng hiện tại
                        Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");

                        // In tin nhắn
                        Console.WriteLine(msg);

                        // Viết lại dòng đang gõ dở
                        Console.Write("> " + currentInput);
                    }
                }
                catch { break; }
            }
        }
    }
}