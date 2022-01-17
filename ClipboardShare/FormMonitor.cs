using SocketCommon;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ClipboardShare
{
    public partial class FormMonitor : Form
    {
        public const int WM_CLIPBOARDUPDATE = 0x031D;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hWnd);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hWnd);

        private int lastTickCount;

        private List<ClipboardData> _lastClipboardDatas = new List<ClipboardData>();

        private ClipboardDataPool _clipboardDataPool;

        private bool _pause;
        public FormMonitor()
        {
            InitializeComponent();
            AddClipboardFormatListener(this.Handle);
            _clipboardDataPool = new ClipboardDataPool(20);
        }

        public bool Pause { get => _pause; set => _pause = value; }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            RemoveClipboardFormatListener(this.Handle);
        }

        internal event EventHandler<ClipboardNotifyEventArgs> ClipboardNotify;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_CLIPBOARDUPDATE && !_pause)
            {
                //列表类解析，会阻止时间，用此法不行，再用lastText方法
                if (Environment.TickCount - this.lastTickCount >= 200)
                {
                    var obj = Clipboard.GetDataObject();
                    var types = obj.GetFormats();
                    foreach (var type in types)
                    {
                        var tmp = _lastClipboardDatas.Find(p => p.Type == type);
                        if (tmp == null || !ClipboardData.Equals(tmp.Data, obj.GetData(type)))
                        {
                            ClipboardModify(obj, types);
                            ClipboardNotify?.BeginInvoke(this, new ClipboardNotifyEventArgs() { Datas = _lastClipboardDatas }, null, null);
                            return;
                        }
                    }
                }
                this.lastTickCount = Environment.TickCount;
                m.Result = IntPtr.Zero;
            }
            base.WndProc(ref m);
        }

        private void ClipboardModify(IDataObject obj, string[] types)
        {
            _lastClipboardDatas.ForEach(p => _clipboardDataPool.Push(p));
            _lastClipboardDatas.Clear();
            GC.Collect();
            foreach (var type in types)
            {
                if (type.Contains("File"))
                    continue;
                var data = _clipboardDataPool.Pull();
                data.Type = type;
                data.Data = obj.GetData(type);
                _lastClipboardDatas.Add(data);
            }
        }
    }

    internal class ClipboardNotifyEventArgs : EventArgs
    {
        public List<ClipboardData> Datas { get; set; }
    }
}
