#nullable enable

using System;
using System.Text;
using System.Windows;
using TcpClient.Modules;
using TcpClient = TcpClient.Modules.TcpClient;
namespace TcpClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ConnectionSettingsManager ConnectionSettingsManager { get; set; }
        private Modules.TcpClient? Client { get; set; }
        public MainWindow()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            InitializeComponent();
            ConnectionSettingsManager = new ConnectionSettingsManager();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ConnectionSettings? settings = ConnectionSettingsManager.GetConnectionSettings();
            IpAddress.Text = settings?.IpAddress ?? "";
            Port.Text = settings?.Port.ToString() ?? "";
            Message.Text = settings?.Message ?? "";
            EndString.Text = settings?.EndString ?? "<EOF>";
        }

        private void SaveConnectionSettings()
        {
            ConnectionSettingsManager.SaveConnectionSettings(new ConnectionSettings()
            {
                IpAddress = IpAddress.Text,
                Port = int.TryParse(Port.Text, out var port) ? port : 0,
                Message = Message.Text,
                EndString = EndString.Text
            });
        }

        private void WriteLog(string message)
        {

            this.Dispatcher.Invoke(() =>
            {
                Status.Text += $"{GetTime()} {message} \n";
                Status.ScrollToEnd();
            });
        }

        private string GetTime()
        {
            return DateTime.Now.ToString("HH:mm:ss");
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            Connect();
        }

        private void Connect()
        {
            var ipAddress = IpAddress.Text;
            if (!int.TryParse(Port.Text, out var port))
            {
                WriteLog("Portに不正な値が指定されています");
                return;
            }

            if (Client != null)
            {
                WriteLog("以前の接続を切断");
                Disconnect();
            }

            Client = new Modules.TcpClient(ipAddress, port);
            WriteLog("接続中");

            Client.OnConnected += (errorMessage) => {
                WriteLog(string.IsNullOrEmpty(errorMessage) ? "接続成功" : errorMessage);
            };
            Client.Connect();
            SaveConnectionSettings();
        }

        private void Send()
        {
            if (Client == null || !Client.Connected)
            {
                WriteLog("先に接続してください");
                return;
            }
            var message = Message.Text;
            var endString = EndString.Text;

            Client.OnSended += (errorMessage) => {
                WriteLog(string.IsNullOrEmpty(errorMessage) ? "送信成功" : errorMessage);
            };

            Client.Send(message + endString);
            SaveConnectionSettings();
        }

        private void Receive()
        {
            if (Client == null || !Client.Connected)
            {
                WriteLog("先に接続してください");
                return;
            }
            Client.OnReceived += (errorMessage, response) => {
                WriteLog(string.IsNullOrEmpty(errorMessage) ? $"レスポンス : {response}" : errorMessage);
            };

            Client.Receive();
            SaveConnectionSettings();
        }
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            Send();
        }
        private void ReceiveButton_Click(object sender, RoutedEventArgs e)
        {
            Receive();
        }
        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            Disconnect();
        }

        private void Disconnect()
        {
            if (Client == null)
                return;

            Client.OnDisconnected += (errorMessage) =>
            {
                WriteLog(string.IsNullOrEmpty(errorMessage) ? "切断しました" : errorMessage);
                Client.Dispose();
                Client = null;
            };
            Client.Disconnect();
        }
    }
}
