using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatGTPClient
{
    internal class Client
    {
        public event EventHandler<string> MessageReceived;
        public event EventHandler Disconnected;
        public event EventHandler<Exception> ExceptionReceived;
        TcpClient client;
        String hostname;
        int port;
        string nickname;
        
        public Client(string nickname, string hostname="127.0.0.1", int port=1234)
        {
            this.nickname = nickname;
            this.hostname = hostname;
            this.port = port;
        }

        public void Start()
        {
            try
            {
                client = new TcpClient(hostname, port);
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                stream.Write(Encoding.ASCII.GetBytes(nickname), 0, nickname.Length);
                stream.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(OnRead), (stream, buffer));
            }
            catch (Exception e)
            {
                ExceptionReceived?.Invoke(this, e);
            }
        }

        public void Stop()
        {
            client.Close();
        }

        public void OnRead(IAsyncResult ar)
        {
            try
            {
                (NetworkStream stream, byte[] buffer) = ((NetworkStream, byte[]))ar.AsyncState;
                int bytesRead = stream.EndRead(ar);
                if (bytesRead == 0)
                {
                    Stop();
                    Disconnected?.Invoke(this, EventArgs.Empty);
                    return;
                }
                string data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                MessageReceived?.Invoke(this, data);
                stream.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(OnRead), (stream, buffer));
            }
            catch (Exception e)
            {
                ExceptionReceived?.Invoke(this, e);
            }
        }

        public void Send(string message)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = Encoding.ASCII.GetBytes(message);
                stream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception e)
            {
                ExceptionReceived?.Invoke(this, e);
            }
        }
    }
}
