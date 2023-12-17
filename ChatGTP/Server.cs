using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Collections;

namespace ChatGTP
{
    internal class Server
    {
        IPAddress localAddr;
        TcpListener server;
        Hashtable clients;
        bool running = false;
        List<TcpClient> admins;
        List<TcpClient> muted;
        public event EventHandler<(TcpClient, string)> ClientConnected;
        public event EventHandler<string> MessageReceived;
        public event EventHandler<Exception> ExceptionReceived;

        private string[] adminNameList = { "admin", "epic_admin", "epicer_admin_with_the_sauce" };
        public Server(string hostname="127.0.0.1", int port = 1234)
        {
            admins = new List<TcpClient>();
            muted = new List<TcpClient>();
            this.localAddr = IPAddress.Parse(hostname);
            this.server = new TcpListener(localAddr, port);
            this.clients = new Hashtable();
        }

        public void Start()
        {
            running = true;
            server.Start();
            server.BeginAcceptTcpClient(new AsyncCallback(OnClientConnect), null);
        }

        public void Stop()
        {
            running = false;
            server.Stop();
            foreach (DictionaryEntry client in clients)
            {
                CloseClient((TcpClient)client.Key);
            }
        }

        public void OnClientConnect(IAsyncResult ar)
        {
            if (!running)
                return;
            TcpClient client = server.EndAcceptTcpClient(ar);
            try
            {
                server.BeginAcceptTcpClient(new AsyncCallback(OnClientConnect), null);
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                stream.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(OnClientRead), (client, buffer));
            }
            catch (Exception e)
            {
                ExceptionReceived?.Invoke(this, e);
                CloseClient(client);
            }
        }

        public void OnClientRead(IAsyncResult ar)
        {
            (TcpClient client, byte[] buffer) = ((TcpClient, byte[]))ar.AsyncState;
            try
            {
                NetworkStream stream = client.GetStream();
                int bytesRead = stream.EndRead(ar);
                if (bytesRead == 0)
                {
                    string nick = clients[client].ToString();
                    CloseClient(client);
                    string msg = FormatMessage("has left the chat", nick, admins.Contains(client), " ");
                    Broadcast(msg);
                    MessageReceived?.Invoke(this, msg);
                    return;
                }
                if (muted.Contains(client))
                {
                    Send("You cannot speak here", client);
                    stream.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(OnClientRead), (client, buffer));
                    return;
                }
                string data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                if (!clients.ContainsKey(client))
                {
                    if (!CheckNickname(data))
                    {
                        CloseClient(client);
                        return;
                    }
                    clients.Add(client, data);
                    ClientConnected?.Invoke(this, (client, data));
                    if (adminNameList.Contains(data))
                    {
                        admins.Add(client);
                    }
                    string nick = clients[client].ToString();
                    string msg = FormatMessage("has joined the chat", nick, admins.Contains(client), " ");
                    Broadcast(msg);
                    MessageReceived?.Invoke(this, msg);
                }
                else
                {
                    MessageReceived?.Invoke(this, FormatMessage(data, clients[client].ToString(), admins.Contains(client)));
                    if (data == "!quit")
                    {
                        CloseClient(client);
                        string msg = FormatMessage("has left the chat", clients[client].ToString(), admins.Contains(client), " ");
                        Broadcast(msg);
                        MessageReceived?.Invoke(this, msg);
                        return;
                    }
                    Broadcast(FormatMessage(data, clients[client].ToString(), admins.Contains(client)));
                    if (data.StartsWith("!kick ") && admins.Contains(client))
                    {
                        string nick = data.Substring(6);
                        foreach (DictionaryEntry c in clients)
                        {
                            if ((string)c.Value == nick)
                            {
                                if (admins.Contains((TcpClient)c.Key))
                                    break;
                                CloseClient((TcpClient)c.Key);
                                string msg = FormatMessage("has been kicked from the chat", nick, false, " ");
                                Broadcast(msg);
                                MessageReceived?.Invoke(this, msg);
                                break;
                            }
                        }
                    }
                    else if (data.StartsWith("!promote ") && admins.Contains(client))
                    {
                        string nick = data.Substring(9);
                        foreach (DictionaryEntry c in clients)
                        {
                            if ((string)c.Value == nick)
                            {
                                if (admins.Contains((TcpClient)c.Key))
                                    break;
                                admins.Add((TcpClient)c.Key);
                                string msg = FormatMessage("has been promoted to admin", nick, true, " ");
                                Broadcast(msg);
                                MessageReceived?.Invoke(this, msg);
                                break;
                            }
                        }
                    }
                    else if (data.StartsWith("!mute ") && admins.Contains(client))
                    {
                        string nick = data.Substring(6);
                        foreach (DictionaryEntry c in clients)
                        {
                            if ((string)c.Value == nick)
                            {
                                if (admins.Contains((TcpClient)c.Key))
                                    break;
                                string msg = FormatMessage("has been muted", nick, false, " ");
                                Broadcast(msg);
                                MessageReceived?.Invoke(this, msg);
                                muted.Add((TcpClient)c.Key);
                                break;
                            }
                        }
                    }
                    else if (data == "!view-managers")
                    {
                        string[] adminNicks = new string[admins.Count];
                        for (int i = 0; i < admins.Count; i++)
                        {
                            adminNicks[i] = clients[admins[i]].ToString();
                        }
                        Send("Managers: " + string.Join(", ", adminNicks), client);
                    }
                }
                stream.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(OnClientRead), (client, buffer));
            }
            catch (Exception e)
            {
                ExceptionReceived?.Invoke(this, e);
                if (client == null || !clients.ContainsKey(client))
                {
                    return;
                }
                string nick = clients[client].ToString();
                CloseClient(client);
                string msg = FormatMessage("has left the chat", nick, admins.Contains(client), " ");
                Broadcast(msg);
                MessageReceived?.Invoke(this, msg);
            }
        }

        public void CloseClient(TcpClient client)
        {
            try
            {
                if (admins.Contains(client))
                    admins.Remove(client);
                if (muted.Contains(client))
                    muted.Remove(client);
                clients.Remove(client);
                client.GetStream().Close();
                client.Close();
            }
            catch (Exception e)
            {
                ExceptionReceived?.Invoke(this, e);
            }
        }

        public void Send(string message, TcpClient client)
        {
            client.GetStream().BeginWrite(Encoding.ASCII.GetBytes(message), 0, message.Length, null, null);
        }

        public void Broadcast(string message)
        {
            foreach (DictionaryEntry client in clients)
            {
                Send(message, (TcpClient)client.Key);
            }
        }

        private string FormatMessage(string message, string nick, bool admin = false, string seperator=": ")
        {
            return DateTime.Now.ToString("HH:mm") + " " + (admin ? "@" : "") + nick + seperator + message;
        }

        private bool CheckNickname(string nickname)
        {
            foreach (char c in nickname)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }
            foreach (DictionaryEntry client in clients)
            {
                if ((string)client.Value == nickname)
                    return false;
            }
            return true;
        }
    }
}
