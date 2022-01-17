using Client;
using Server;
using SocketCommon;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace ClipboardShare
{
    public partial class FormMain : Form
    {
        private FormMonitor _monitor;
        private ClientSocketManager _sm;
        private ServerSocketManager _serverSM;

        private string _serverIP = "";
        private int _serverPort = 0;

        private bool _selfServer;

        private bool _isStart = false;

        private TransferStructure _currentTransfer;
        public FormMain()
        {
            InitializeComponent();

            InitServer();
        }

        private void InitServer()
        {
            var keys = ConfigurationManager.AppSettings.AllKeys;
            if (!keys.Contains("ServerIP") || !keys.Contains("ServerPort"))
            {
                MessageBox.Show("配置文件缺少服务器节点");
                Environment.Exit(0);
            }

            if (keys.Contains("SelfServer"))
                _selfServer = ConfigurationManager.AppSettings["SelfServer"].ToString().ToLower() == "true";

            _serverIP = ConfigurationManager.AppSettings["ServerIP"].ToString();
            var valid = int.TryParse(ConfigurationManager.AppSettings["ServerPort"].ToString(), out _serverPort);
            if (!valid)
            {
                MessageBox.Show("服务器端口必须为数字");
                Environment.Exit(0);
            }

            if (_selfServer)
            {
                _serverSM = new ServerSocketManager(100, 2048);
                _serverSM.Init();
                _serverSM.Start(new IPEndPoint(IPAddress.Any, _serverPort));
            }
        }

        private void Connect()
        {
            try
            {
                _sm = new ClientSocketManager(_serverIP, _serverPort);
                var error = _sm.Connect(this.tbxGroupId.Text);
                if (error != System.Net.Sockets.SocketError.Success)
                    throw new Exception("服务器连接失败");
                _sm.ServerDataHandler += sm_ServerDataHandler;
                _sm.ServerStopEvent += sm_ServerStopEvent;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Environment.Exit(0);
            }
        }

        private void StartMonitor()
        {
            _monitor = new FormMonitor();
            _monitor.ClipboardNotify += monitor_ClipboardNotify;
            this.Connect();

            this.btnMonitor.Text = "关闭同步";
            this.labState.ForeColor = Color.Green;
            this.labState.Text = "● 同步已启动";
            this.tbxGroupId.Enabled = !_isStart;
        }

        private void StopMonitor()
        {
            _monitor.ClipboardNotify -= monitor_ClipboardNotify;
            _monitor.Close();
            this.Disconnect();

            this.btnMonitor.Text = "开启同步";
            this.labState.ForeColor = Color.Red;
            this.labState.Text = "● 未启动同步";
            this.tbxGroupId.Enabled = !_isStart;
        }

        private void Disconnect()
        {
            if (_sm != null)
            {
                _sm.ServerDataHandler -= sm_ServerDataHandler;
                _sm.ServerStopEvent -= sm_ServerStopEvent;
                _sm.Disconnect();
                _sm.Dispose();
                _sm = null;
                GC.Collect();
            }
        }

        private void SetDataToClipboard(List<ClipboardData> datas)
        {
            IDataObject data = new DataObject();
            foreach (var item in datas)
                data.SetData(item.Type, false, item.Data);

            this.BeginInvoke((MethodInvoker)delegate
            {
                _monitor.Pause = true;
                Clipboard.SetDataObject(data, false);
                Application.DoEvents();
                _monitor.Pause = false;
            });
        }
        private void sm_ServerStopEvent()
        {
            MessageBox.Show("服务器已退出");
            Environment.Exit(0);
        }
        private void sm_ServerDataHandler(TransferStructure stru)
        {
            SetDataToClipboard(stru.Data as List<ClipboardData>);
        }

        private void btnMonitor_Click(object sender, EventArgs e)
        {
            if (!_isStart && string.IsNullOrEmpty(this.tbxGroupId.Text))
            {
                MessageBox.Show("组Id不能为空");
                return;
            }

            _isStart = !_isStart;

            if (_isStart)
                StartMonitor();
            else
                StopMonitor();
        }

        private void monitor_ClipboardNotify(object sender, ClipboardNotifyEventArgs e)
        {
            if (this.cbxWithoutSync.Checked)
                return;

            _currentTransfer = null;
            _currentTransfer = new TransferStructure() { TransferProtocol = TransferProtocol.SyncClipboard, Data = e.Datas };
            _sm.Send(_currentTransfer);
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;

            this.Hide();
            this.notifyIcon1.Visible = true;
            this.notifyIcon1.ShowBalloonTip(1000, "", "运行中", ToolTipIcon.Info);
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
        }

        private void menuExit_Click(object sender, EventArgs e)
        {
            this.notifyIcon1.Dispose();
            Environment.Exit(0);
        }
    }
}
