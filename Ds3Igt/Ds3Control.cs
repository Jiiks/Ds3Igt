using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Win32;

namespace Ds3Igt
{
    public partial class Ds3Control : UserControl
    {

        private string _ds3Path;
        private bool _ds3Located, _modExists;
        private Ds3AutoSplitterSettingsHandler _autoSplitterSettingsHandler;

        public Ds3Control()
        {
            InitializeComponent();
            LocateDs3();
            _autoSplitterSettingsHandler = new Ds3AutoSplitterSettingsHandler(this.splitSettings.Nodes, this.cb_autoSplit, this.cb_autoStartTimer);
            label4.Text = $"Dark Souls 3 IGT Timer v{Config.Version} by Jiiks";
        }

        private void LocateDs3(string basePath = null)
        {
            try
            {
                btnNoLogo.Enabled = false;
                btnUninstallNoLogo.Enabled = false;
                _modExists = false;
                _ds3Path = basePath;

                if (basePath == null)
                {

                    string SteamPath = null;
                    using (var key = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam"))
                    {
                        var o = key?.GetValue("SteamPath");
                        if (o != null)
                        {
                            SteamPath = o as string;
                        }
                    }

                    if (SteamPath == null)
                    {
                        lblStatus.Text = "Status: Failed to locate DS3!";
                        lblStatus.ForeColor = Color.Red;
                        btnNoLogo.Text = "Locate DS3";
                        btnNoLogo.Enabled = true;
                        return;
                    }

                    _ds3Path = $"{SteamPath}\\SteamApps\\common\\DARK SOULS III\\Game";
                }

                _ds3Path = _ds3Path.EndsWith("\\Game") ? _ds3Path : $"{_ds3Path}\\Game";

                if (!Directory.Exists(_ds3Path))
                {
                    lblStatus.Text = "Status: Failed to locate DS3!";
                    lblStatus.ForeColor = Color.Red;
                    btnNoLogo.Text = "Locate DS3";
                    btnNoLogo.Enabled = true;
                    return;
                }

                lblStatus.Text = "Status: Located DS3!";

                if (File.Exists($"{_ds3Path}\\dinput8.dll"))
                {
                    lblStatus.Text = "Status: Mod installed!";
                    _modExists = true;
                    btnUninstallNoLogo.Enabled = true;
                    lblStatus.ForeColor = Color.Green;
                }
                else
                {
                    lblStatus.Text = "Status: Mod not installed!";
                    lblStatus.ForeColor = Color.Orange;
                }

                btnNoLogo.Text = "Install/Update";
                _ds3Located = true;
                btnNoLogo.Enabled = true;
            }
            catch (Exception e)
            {
                lblStatus.Text = "Unknown error";
            }
        }

        private void DownloadMod()
        {
            try
            {
                lblStatus.Text = "Status: Downloading Mod";
                using (var wc = new WebClient())
                {
                    wc.DownloadFile("http://speedsouls.com/jiiks/nologo/DINPUT8.DLL", $"{_ds3Path}\\DINPUT8.dll");
                }
                lblStatus.Text = "Status: Mod installed!";
                lblStatus.ForeColor = Color.Green;
                btnUninstallNoLogo.Enabled = true;
            }
            catch (Exception ee)
            {
                lblStatus.Text = "Unknown error";
            }
        }

        public XmlNode GetSettings(XmlDocument doc) => doc.CreateElement("Settings");

        public void SetSettings(XmlNode node) { }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://speedsouls.com/");
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://jiiks.net/");
        }

        private void btnNoLog_Click(object sender, EventArgs e)
        {
            if (!_ds3Located)
            {
                var dialog = new FolderBrowserDialog();
                dialog.ShowDialog();
                LocateDs3(dialog.SelectedPath);
                return;
            }
            DownloadMod();
        }

        private void linkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/bladecoding");
        }

        private void linkLabel4_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://speedsouls.com/darksouls3:No-Logo_Mod");
        }


        [DllImport("user32.dll", EntryPoint = "SetWindowText")]
        private static extern int SetWindowText(IntPtr hWnd, string text);

        [DllImport("user32.dll", EntryPoint = "FindWindowEx")]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("User32.dll", EntryPoint = "SendMessage")]
        private static extern int SendMessage(IntPtr hWnd, int uMsg, int wParam, string lParam);

        private void button1_Click(object sender, System.EventArgs e)
        {
            try
            {
                var notepad = Process.Start(new ProcessStartInfo("notepad.exe"));
                if (notepad == null)
                {
                    MessageBox.Show("Failed to load readme! Check the info instead", "ERROR", MessageBoxButtons.OK);
                    return;
                }
                notepad.WaitForInputIdle();
                SetWindowText(notepad.MainWindowHandle, "readme");
                var child = FindWindowEx(notepad.MainWindowHandle, new IntPtr(0), "Edit", null);
                SendMessage(child, 0x000C, 0,
                    "== Installing ==\r\n * Extract DINPUT8.dll to the DarkSoulsIII.exe directory. \r\n   E.g. \"C:\\Program Files (x86)\\Steam\\steamapps\\common\\DARK SOULS III\\Game\".\r\n\r\n== What is this? ==\r\nThis is a DS3 mod that removes the intro logo screens from the game.\r\nThe logos shown when first starting DS3 and after saving/quiting.\r\n\r\n\r\n== Supported Versions ==\r\nv1.09\r\nv1.08\r\nv1.04\r\n\r\n== Help ==\r\n\r\nQ: The intro logo screens are still showing\r\nA: Either the modified DINPUT8.dll is not in the correct directory or you are playing an unsupported version of the game.\r\n\r\n== Source ==\r\nhttps://github.com/bladecoding/DarkSouls3RemoveIntroScreens\r\n");

            }
            catch (Exception ee)
            {
                lblStatus.Text = "Unknown error";
            }
        }

        private void label7_Click(object sender, EventArgs e)
        {

        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
        }

        private void groupBox4_Enter(object sender, EventArgs e)
        {

        }

        private void treeView2_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_autoSplitterSettingsHandler != null)
                _autoSplitterSettingsHandler.set(e.Node.Name, e.Node.Checked);
        }

        private void treeView2_BeforeCheck(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Action == TreeViewAction.Unknown)
                return;
            if (SystemColors.GrayText == e.Node.ForeColor) { 
                e.Cancel = true;
                return;
                }
            //disable children if parent is disabled
            if (e.Node.Level == 0)
            {
                Color color;
                if (e.Node.Checked) {
                    color = SystemColors.GrayText;
                }
                else{
                    color = SystemColors.MenuText;  
                }
                for (int i = 0; i < e.Node.Nodes.Count; i++)
                {
                    e.Node.Nodes[i].ForeColor = color;
                }
            }
            else
            {
                TreeNode parent = e.Node.Parent;
                if (parent.Name != "Misc")
                {
                    if (e.Node.Checked)
                    {
                        e.Cancel = true;
                        return;
                    }
                    //make only one child selectable
                    for (int i = 0; i < parent.Nodes.Count; i++)
                    {
                        parent.Nodes[i].Checked = false;
                    }
                    return;
                }
            }

        }

        private void Ds3Control_Load(object sender, EventArgs e)
        {

        }

        private void cb_autoStartTimer_CheckedChanged(object sender, EventArgs e)
        {
            if(_autoSplitterSettingsHandler != null)
                _autoSplitterSettingsHandler.set(cb_autoStartTimer.Name, cb_autoStartTimer.Checked);
        }

        private void cb_autoSplit_CheckedChanged(object sender, EventArgs e)
        {
            if (_autoSplitterSettingsHandler != null)
                _autoSplitterSettingsHandler.set(cb_autoSplit.Name, cb_autoSplit.Checked);
        }

        private void btnUninstallNoLogo_Click(object sender, EventArgs e)
        {
            try
            {
                if (!File.Exists($"{_ds3Path}\\dinput8.dll")) return;
                File.Delete($"{_ds3Path}\\dinput8.dll");
                LocateDs3();
            }
            catch (Exception ee)
            {
                lblStatus.Text = "Unknown error";
            }
        }
    }
}
