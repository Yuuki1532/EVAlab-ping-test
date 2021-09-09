using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.NetworkInformation;

namespace EVAlab_ping_test {
    using ExtensionMethods;
    public partial class MainForm : Form {
        public MainForm() {
            InitializeComponent();
        }

        int pingTimeout = 1000; // ms
        int pingInterval = 2000; // ms
        volatile bool isTesting = false;
        volatile bool isStopping = false;
        volatile bool isFormClosing = false;

        private void MainForm_Load(object sender, EventArgs e) {

            // read ip list from file 'ip_list.txt'
            var listIP = System.IO.File.ReadAllLines(@"ip_list.txt");
            // add ip to list
            foreach (string ip in listIP) {
                var item = new ListViewItem("●");
                item.SubItems.Add(ip);
                item.SubItems.Add("N/A");
                listViewStatus.Items.Add(item);
            }

            // avoid flickering by extension methods (reflection)
            listViewStatus.DoubleBuffering(true);
        }

        private async void buttonStart_Click(object sender, EventArgs e) {
            isTesting = true;
            buttonStop.Enabled = true;
            buttonStart.Enabled = false;

            List<Ping> pingSenders = new List<Ping>();

            for (int i = 0; i < listViewStatus.Items.Count; i++) {
                pingSenders.Add(new Ping());
            }

            while (isTesting) {
                List<Task> subTasks = new List<Task>();

                subTasks.Add(Task.Delay(pingInterval));

                for (int i = 0; i < listViewStatus.Items.Count; i++) {
                    ListViewItem item = listViewStatus.Items[i];
                    Ping pingSender = pingSenders[i];

                    subTasks.Add(Task.Run(() => {
                        bool isSuccessful = false;
                        string textResult;

                        try {
                            var reply = pingSender.Send(item.SubItems[1].Text, pingTimeout);
                            if (reply.Status == IPStatus.Success) {
                                isSuccessful = true;
                                textResult = $"{reply.RoundtripTime} ms";
                            }
                            else {
                                isSuccessful = false;
                                textResult = Enum.GetName(typeof(IPStatus), reply.Status);
                            }
                        }
                        catch (Exception ex) {
                            isSuccessful = false;
                            textResult = $"Failed: {ex.Message}";
                        }

                        Invoke(new Action(() => {
                            item.SubItems[2].Text = textResult;

                            if (isSuccessful) {
                                item.ForeColor = Color.Green;
                            }
                            else {
                                item.ForeColor = Color.Red;
                            }
                        }));

                    }));

                }

                await Task.WhenAll(subTasks);
            }

            foreach (Ping pingSender in pingSenders) {
                pingSender.Dispose();
            }

            buttonStart.Enabled = true;
            isStopping = false;
            buttonStop.Text = "Stop";
            if (isFormClosing) {
                this.Close();
            }
        }

        private void buttonStop_Click(object sender, EventArgs e) {
            buttonStop.Enabled = false;
            buttonStop.Text = "Waiting";

            isTesting = false; // note: atomic
            isStopping = true;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e) {
            this.Text = "Waiting";
            buttonStop.Enabled = false;
            
            if (isStopping) {
                isFormClosing = true;
                e.Cancel = true; // handled by button click event
            }
            else if (isTesting) {
                isTesting = false; // note: atomic
                isStopping = true;
                isFormClosing = true;
                e.Cancel = true;
            }
        }

    }
}

namespace ExtensionMethods {
    using System.Reflection;
    public static class ControlExtensions {
        public static void DoubleBuffering(this Control control, bool enable) {
            var doubleBufferPropertyInfo = control.GetType().GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            doubleBufferPropertyInfo.SetValue(control, enable, null);
        }
    }
}
