using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Ds3Igt
{
    class Ds3AutoSplitterSettingsHandler
    {
        private TreeNodeCollection _settingsTree;
        private Dictionary<string, string> _autosplitterSettings;
        private string configFilepath;

        public Ds3AutoSplitterSettingsHandler(TreeNodeCollection settingsTree, CheckBox cb_autoSplit, CheckBox cb_autoStartTimer)
        {
            configFilepath = Path.Combine(Directory.GetCurrentDirectory(), "Components", "Ds3AutosplitterSettings.xml");
            _settingsTree = settingsTree;

            if (File.Exists(configFilepath))
            {
                _autosplitterSettings = XElement.Parse(File.ReadAllText(configFilepath))
                                        .Elements().ToDictionary(k => k.Name.ToString(), v => v.Value.ToString());
                initilizeTreeViewChecked(_settingsTree);
                initilizeCheckBoxes(cb_autoSplit, cb_autoStartTimer);
            }
            else {
                _autosplitterSettings = new Dictionary<string, string>();
            }
        }

        private void saveSettingsDictionary()
        {
            new XElement("root", _autosplitterSettings.Select(kv => new XElement(kv.Key, kv.Value)))
                .Save(configFilepath, SaveOptions.OmitDuplicateNamespaces);
        }

        public void set(string key, bool state)
        {
            if (_autosplitterSettings.ContainsKey(key))
            {
                _autosplitterSettings[key] = Convert.ToString(state);
            }else { 
                _autosplitterSettings.Add(key, Convert.ToString(state));
            }
            saveSettingsDictionary();
        }

        private void initilizeTreeViewChecked(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (_autosplitterSettings.ContainsKey(node.Name))
                    node.Checked = Convert.ToBoolean(_autosplitterSettings[node.Name]);
                initilizeTreeViewChecked(node.Nodes);
            }
        }

        private void initilizeCheckBoxes(CheckBox cb_autoSplit, CheckBox cb_autoStartTimer)
        {
            if (_autosplitterSettings.ContainsKey(cb_autoSplit.Name))
                cb_autoSplit.Checked = Convert.ToBoolean(_autosplitterSettings[cb_autoSplit.Name]);
            if (_autosplitterSettings.ContainsKey(cb_autoStartTimer.Name)) { }
                cb_autoStartTimer.Checked = Convert.ToBoolean(_autosplitterSettings[cb_autoStartTimer.Name]);
        }
    }
}
