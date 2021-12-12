using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpClient.Modules
{
    public class StateObject
    {
        // Client socket.  
        public Socket workSocket = null;
        // Size of receive buffer.  
        public const int BufferSize = 256;
        // Receive buffer.  
        public byte[] buffer = new byte[BufferSize];
        // Received data string.  
        public StringBuilder sb = new StringBuilder();
    }

    public class TcpClient : IDisposable
    {
        private const int DEFAULT_TIMEOUT_MILLISEC = 5000;
        private ManualResetEvent connectDone = new ManualResetEvent(false);
        private ManualResetEvent sendDone = new ManualResetEvent(false);
        private ManualResetEvent receiveDone = new ManualResetEvent(false);
        private ManualResetEvent disconnectDone = new ManualResetEvent(false);

        private string _response;
        private string _isReceiveErrorMessage;
        private string _isSendErrorMessage;
        private string _isConnectErrorMessage;
        private string _isDisconnectErrorMessage;

        private Socket? _client;

        public delegate void ConnectedEventHandler(string errorMessage);
        public delegate void SendedEventHandler(string errorMessage);
        public delegate void ReceivedEventHandler(string errorMessage, string response);
        public delegate void DisconnectedEventHandler(string errorMessage);

        public event ConnectedEventHandler? OnConnected = null;
        public event SendedEventHandler? OnSended = null;
        public event ReceivedEventHandler? OnReceived = null;
        public event DisconnectedEventHandler? OnDisconnected = null;

        private IPEndPoint Endpoint { get; }

        public TcpClient(IPAddress address, int port) : this(new IPEndPoint(address, port)) { }

        public TcpClient(string address, int port) : this(new IPEndPoint(IPAddress.Parse(address), port)) { }

        public TcpClient(IPEndPoint endpoint)
        {
            Endpoint = endpoint;
        }

        private void Initialize()
        {
            _response = string.Empty;
            _isReceiveErrorMessage = string.Empty;
            _isSendErrorMessage = string.Empty;
            _isConnectErrorMessage = string.Empty;
            _client = null;
        }

        public bool Connected => _client != null && _client.Connected;

        public TcpClient Connect(int timeout = DEFAULT_TIMEOUT_MILLISEC)
        {
            Initialize();
            // Create a TCP/IP socket.  
            _client = new Socket(Endpoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            // コネクションリクエスト(ただのConnectでもいいが、非同期の要望があったときのため)
            IAsyncResult result = _client.BeginConnect(Endpoint,
                new AsyncCallback(ConnectCallback), _client);

            return this;
        }

        /// <summary>
        /// Connection結果を待機する
        /// 呼び出し元のThreadをブロックします。ブロックしたくない場合はOnConnectedイベントを利用してください。
        /// </summary>
        /// <returns></returns>
        public string ConnectWait()
        {
            connectDone.WaitOne();
            return _isConnectErrorMessage;
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // 状態オブジェクトからSocketを再取得
                Socket client = (Socket)ar.AsyncState;

                // 保留中の非同期接続要求を終了します
                client.EndConnect(ar);

                // 接続が確立したことを通知します.  
                connectDone.Set();
                OnConnected?.Invoke(string.Empty);
            }
            catch (Exception e)
            {
                connectDone.Set();
                OnConnected?.Invoke(e.ToString());
            }
        }

        public TcpClient? Send(string message, int timeout = DEFAULT_TIMEOUT_MILLISEC)
        {
            if (_client == null || !_client.Connected)
            {
                Console.WriteLine("先にサーバーとConnectionしてください");
                return null;
            }
            _client.SendTimeout = timeout;
            _isSendErrorMessage = string.Empty;

            // ASCIIエンコーディングを使用して文字列データをバイトデータに変換します。
            byte[] byteData = Encoding.ASCII.GetBytes(message);

            // リモートデバイスへのデータの送信を開始します。
            _client.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), _client);

            return this;
        }

        /// <summary>
        /// Send結果を待機する
        /// 呼び出し元のThreadをブロックします。ブロックしたくない場合はOnSendedイベントを利用してください。
        /// </summary>
        /// <returns></returns>
        public string SendWait()
        {
            sendDone.WaitOne();
            return _isSendErrorMessage;
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // 状態オブジェクトからSocketを再取得
                Socket client = (Socket)ar.AsyncState;

                // リモートデバイスへのデータの送信を完了します。
                int bytesSent = client.EndSend(ar);

                // すべてのバイトが送信されたことを通知します。
                sendDone.Set();
                // この非同期Threadでコールバックを実行
                OnSended?.Invoke(string.Empty);
            }
            catch (Exception e)
            {
                _isSendErrorMessage = e.ToString();
                // すべてのバイトが送信されたことを通知します。
                sendDone.Set();
                OnSended?.Invoke(_isSendErrorMessage);
            }

        }

        /// <summary>
        /// サーバーからのレスポンスを待機します。
        /// ReaceiveWaitでレスポンスを待機し、結果を取得できます。
        /// </summary>
        /// <returns></returns>
        public TcpClient? Receive(int timeout = DEFAULT_TIMEOUT_MILLISEC)
        {
            if (_client == null || !_client.Connected)
            {
                Console.WriteLine("先にサーバーとConnectionしてください");
                return null;
            }
            _client.ReceiveTimeout = timeout;
            _response = string.Empty;
            _isReceiveErrorMessage = string.Empty;

            // 状態オブジェクトを作成します。
            StateObject state = new StateObject();
            state.workSocket = _client;

            // リモートデバイスからのデータの受信を開始します。
            _client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReceiveCallback), state);
            return this;
        }

        /// <summary>
        /// 受信結果を待機して、受信結果を返します
        /// 呼び出し元のThreadをブロックします。ブロックしたくない場合はOnReceivedイベントを利用してください。
        /// </summary>
        /// <returns></returns>
        public (string, string) ReceiveWait()
        {
            receiveDone.WaitOne();
            return (_isReceiveErrorMessage, _response);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // 非同期状態オブジェクトから状態オブジェクトとクライアントソケットを取得します。
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                // リモートデバイスからデータを読み取ります。
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // より多くのデータがある可能性があるため、これまでに受信したデータを保存します。
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    // 残りのデータを取得します。
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    // すべてのデータが到着しました。それに応じて。
                    if (state.sb.Length > 1)
                    {
                        _response = state.sb.ToString();
                    }
                    // すべてのバイトが受信されたことを通知します。
                    receiveDone.Set();
                    // この非同期Threadでコールバックを実行
                    OnReceived?.Invoke(string.Empty, _response);
                }
            }
            catch (Exception e)
            {
                _response = string.Empty;
                _isReceiveErrorMessage = e.ToString();
                // すべてのバイトが受信されたことを通知します。
                receiveDone.Set();
                OnReceived?.Invoke(_isReceiveErrorMessage, string.Empty);
            }
        }

        public void Disconnect()
        {
            if (_client == null || !_client.Connected)
            {
                return;
            }

            StateObject state = new StateObject();
            state.workSocket = _client;
            _client.BeginDisconnect(false, new AsyncCallback(DisconnectCallback), state);
        }

        public string DisconnectWait()
        {
            disconnectDone.WaitOne();
            return _isDisconnectErrorMessage;
        }

        private void DisconnectCallback(IAsyncResult ar)
        {
            try
            {
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                client.EndDisconnect(ar);

                disconnectDone.Set();
                OnDisconnected?.Invoke(string.Empty);
            }
            catch (Exception e)
            {
                _isDisconnectErrorMessage = e.ToString();
                OnDisconnected?.Invoke(e.ToString());
            }
        }

        public void Dispose()
        {

            if (_client != null)
            {
                _client.Close();
                _client = null;
            }
            OnConnected = null;
            OnSended = null;
            OnReceived = null;
        }
    }
}
