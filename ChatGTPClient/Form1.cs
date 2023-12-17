using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChatGTPClient
{
    public partial class Form1 : Form
    {
        bool connected = false;
        Client client;
        public Form1()
        {
            InitializeComponent();
            sendBtn.Enabled = false;
            messageTextBox.Enabled = false;
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (!connected)
            {
                Input input = new Input();
                input.ShowDialog();
                if (input.DialogResult == DialogResult.OK)
                {
                    string address = input.addressBox.Text;
                    int port = int.Parse(input.portBox.Text);
                    string nickname = input.nickBox.Text;
                    client = new Client(nickname, address, port);
                    client.MessageReceived += (s, msg) =>
                    {
                        messageListBox.Items.Add(msg);
                    };
                    client.ExceptionReceived += (s, err) =>
                    {
                        MessageBox.Show(err.Message);
                        connected = false;
                        sendBtn.Enabled = false;
                        messageTextBox.Enabled = false;
                        btnConnect.Text = "Connect";
                    };
                    client.Disconnected += (s, err) =>
                    {
                        connected = false;
                        sendBtn.Enabled = false;
                        messageTextBox.Enabled = false;
                        btnConnect.Text = "Connect";
                        MessageBox.Show("Disconnected");
                    };
                    messageListBox.Items.Clear();
                    connected = true;
                    sendBtn.Enabled = true;
                    messageTextBox.Enabled = true;
                    btnConnect.Text = "Disconnect";
                    client.Start();
                }
                else
                {
                    MessageBox.Show("Invalid input");
                }
            }
            else
            {
                client.Stop();
                connected = false;
                sendBtn.Enabled = false;
                messageTextBox.Enabled = false;
                btnConnect.Text = "Connect";
            }
        }

        private void messageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { Send(); }
        }

        private void sendBtn_Click(object sender, EventArgs e)
        {
            Send();
        }

        private void Send()
        {
            if (messageTextBox.Text == "") { return; }
            client.Send(messageTextBox.Text);
            messageTextBox.Text = "";
        }
    }
}
