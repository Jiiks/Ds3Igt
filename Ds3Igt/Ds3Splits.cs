using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using LiveSplit.ComponentUtil;
using LiveSplit.Model;
using System.Windows.Forms;

namespace Ds3Igt
{
    public class Ds3Splits
    {
        private class PlayerPos
        {
            private MemoryWatcherList coord;

            public PlayerPos(int baseOffset, int xOffset)
            {
                coord = new MemoryWatcherList();
                coord.Add(new MemoryWatcher<float>(new DeepPointer(baseOffset, new int[] { xOffset + 0x00 })));
                coord.Add(new MemoryWatcher<float>(new DeepPointer(baseOffset, new int[] { xOffset + 0x04 })));
                coord.Add(new MemoryWatcher<float>(new DeepPointer(baseOffset, new int[] { xOffset + 0x08 })));
            }
            public PlayerPos(int baseOffset, int[] xOffsets)
            {
                coord = new MemoryWatcherList();
                coord.Add(new MemoryWatcher<float>(new DeepPointer(baseOffset, xOffsets)));
                xOffsets[xOffsets.Length - 1] = xOffsets[xOffsets.Length - 1] + 0x04;
                coord.Add(new MemoryWatcher<float>(new DeepPointer(baseOffset, xOffsets)));
                xOffsets[xOffsets.Length - 1] = xOffsets[xOffsets.Length - 1] + 0x04;
                coord.Add(new MemoryWatcher<float>(new DeepPointer(baseOffset, xOffsets)));
            }

            public void update(Process dsProcess){ coord.UpdateAll(dsProcess);}
            public float get_x(Process dsProcess) { return (float)coord[0].Current; }
            public float get_y(Process dsProcess) { return (float)coord[2].Current; }
            public float get_z(Process dsProcess) { return (float)coord[1].Current; }
        }

        private class BoundingBox
        {
            private float _minX, _minY, _minZ, _maxX, _maxY, _maxZ;
            public BoundingBox(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
            {
                _minX = minX; _minY = minY; _minZ = minZ; _maxX = maxX; _maxY = maxY; _maxZ = maxZ;
            }
            public bool intersect(Process dsProcess, PlayerPos pp)
            {
                pp.update(dsProcess);
                if (pp.get_x(dsProcess) >= _minX &&
                    pp.get_x(dsProcess) <= _maxX &&
                    pp.get_y(dsProcess) >= _minY &&
                    pp.get_y(dsProcess) <= _maxY &&
                    pp.get_z(dsProcess) >= _minZ &&
                    pp.get_z(dsProcess) <= _maxZ)
                {
                    return true;
                }
                return false;
            }
        }

        private interface ISplit
        {
            bool split(Process dsProcess, TimerModel timer, PlayerPos playerPos, out bool loadSplitQueued, out bool finalSplitFlag);
            void init(Process dsProcess);
        }

        //WorldFlagSplit
        private class WFSplit : ISplit
        {
            private string _key;
            private int _bitOffset;
            private bool _enabled;
            private bool _splitLevelOneOnLoad;
            private MemoryWatcher _memWatcher;
            private TreeNode _settingsNode;

            public WFSplit(string key, TreeView settings, IntPtr baseAddress, int offsets, byte bitOffset)
            {
                _key = key;
                _bitOffset = bitOffset;
                _memWatcher = new MemoryWatcher<byte>(baseAddress + offsets);
                _enabled = true;
                _splitLevelOneOnLoad = true;
                _settingsNode = settings.Nodes.Find(key, true)[0];
            }

            public WFSplit(string key, TreeView settings, IntPtr baseAddress, int offsets, byte bitOffset, bool splitLevelOneOnLoad) :  this(key, settings, baseAddress, offsets, bitOffset)
            {
                _splitLevelOneOnLoad = splitLevelOneOnLoad;
            }

            public void init(Process dsProcess)
            {
                _memWatcher.Update(dsProcess);
                _enabled = !(((Convert.ToByte(_memWatcher.Current) >> _bitOffset) & 1) == 1);
            }

            public bool split(Process dsProcess, TimerModel timer, PlayerPos playerPos, out bool loadSplitQueued, out bool finalSplitFlag)
            {
                loadSplitQueued = false;
                finalSplitFlag = false;
                if (!_enabled)
                    return false;

                _memWatcher.Update(dsProcess);
                if (_settingsNode.Checked && ((Convert.ToByte(_memWatcher.Old) >> _bitOffset) & 1) == 0 && ((Convert.ToByte(_memWatcher.Current) >> _bitOffset) & 1) == 1)
                {
                    Trace.WriteLine("memory watcher changed: " + _key);
                    if (_settingsNode.Level == 0)
                    {
                        Trace.WriteLine("_settingsNode.Level == 0");
                        if (_settingsNode.Nodes[0].Checked) { Trace.WriteLine("split: " + _key); timer.Split(); _enabled = false; return true; }
                        if (_splitLevelOneOnLoad) {
                            if (_settingsNode.Nodes[1].Checked) { Trace.WriteLine("queued split: " + _key); loadSplitQueued = true; _enabled = false;
                                if (_settingsNode.Nodes[1].Parent.Name == "SoulofCinder") {
                                    loadSplitQueued = false;
                                    finalSplitFlag = true;
                                }
                                return true; }
                        }
                    }
                    else
                    {
                        Trace.WriteLine("_settingsNode.Level == 1");
                        Trace.WriteLine("node key " + _settingsNode.Name);
                        if (_settingsNode.Parent.Checked)
                        {
                            if (_splitLevelOneOnLoad) { Trace.WriteLine("queued split" + _key); loadSplitQueued = true; _enabled = false; return true; }
                            Trace.WriteLine("split: " + _key); timer.Split(); _enabled = false; return true;
                        }
                    }
                }
                return false;
            }
        }

        private class BBSplit : ISplit
        {
            private string _key;
            private bool _enabled;
            private TreeNode _settingsNode;
            private BoundingBox _bb;

            public BBSplit(string key, TreeView settings, BoundingBox bb) {
                _key = key;
                _enabled = true;
                _settingsNode = settings.Nodes.Find(key, true)[0];
                _bb = bb;
            }

            public void init(Process dsProcess) { }

            public bool split(Process dsProcess, TimerModel timer, PlayerPos playerPos, out bool loadSplitQueued, out bool finalSplitFlag)
            {
                loadSplitQueued = false;
                finalSplitFlag = false;
                if (_enabled && _settingsNode.Checked) {
                    if(_bb.intersect(dsProcess, playerPos)) { Trace.WriteLine("BBsplit"); timer.Split(); _enabled = false; return true; }
                }
                return false;
            }
        }

        private class UpgradeSplit : ISplit
        {
            private string _key;
            private bool _enabled;
            private int _upgradeLevel;
            private TreeNode _settingsNode;
            MemoryWatcher<int> _maxUpgradeLevel;

            public UpgradeSplit(string key, TreeView settings, IntPtr maxUpgradeAddress, int upgradeLevel)
            {
                _key = key;
                _enabled = true;
                _settingsNode = settings.Nodes.Find(key, true)[0];
                _upgradeLevel = upgradeLevel;
                _maxUpgradeLevel = new MemoryWatcher<int>(maxUpgradeAddress);
            }

            public void init(Process dsProcess)
            {
                _maxUpgradeLevel.Update(dsProcess);
                if (_maxUpgradeLevel.Current >= _upgradeLevel)
                    _enabled = false;
            }

            public bool split(Process dsProcess, TimerModel timer, PlayerPos playerPos, out bool loadSplitQueued, out bool finalSplitFlag)
            {
                loadSplitQueued = false;
                finalSplitFlag = false;
                if (_enabled && _settingsNode.Checked)
                {
                    _maxUpgradeLevel.Update(dsProcess);
                    if (_maxUpgradeLevel.Current == _upgradeLevel)
                    {
                        Trace.WriteLine("UpgradeSplit queued: +" + _upgradeLevel);
                        loadSplitQueued = true;
                        _enabled = false;
                        return true;
                    }
                    return false;
                }
                return false;
            }
        }

        private List<ISplit> splits;
        private PlayerPos _playerPos;
        private bool initilized;

        public Ds3Splits(Process dsProcess, IntPtr worldFlagPointer, TreeView settings)
        {
            initilized = false;
            IntPtr maxUpgradePtr = (IntPtr) null;
            splits = new List<ISplit> { };

            switch (dsProcess.MainModule.FileVersionInfo.FileVersion)
            {
                case "1.8.0.0": // "App v1.08, Reg v1.22"
                    _playerPos = new PlayerPos(0x4703DF8, 0xA74);
                    splits.Add(new WFSplit("PerimeterBonfire", settings, worldFlagPointer, 0x2F02, 0x07, false));
                    break;
                case "1.11.0.0": // "App v1.11, Reg v1.30"
                    _playerPos = new PlayerPos(0x476D190, 0x50);
                    break;
                case "1.12.0.0": // "App v1.12, Reg v1.31"
                default:
                    maxUpgradePtr = (IntPtr) 0x7FF51C5D1F04;
                    _playerPos = new PlayerPos(0x4763518, new int[] { 0x40, 0x28, 0x80 });
                    splits.Add(new WFSplit("PerimeterBonfire", settings, worldFlagPointer, 0x2D02, 0x07, false));
                    break;
            }

            splits.Add(new WFSplit("Gundyr",              settings, worldFlagPointer, 0x5A67,   0x07));
            splits.Add(new WFSplit("Greatwood",           settings, worldFlagPointer, 0x1967,   0x07));
            splits.Add(new WFSplit("Vordt",               settings, worldFlagPointer, 0xF67,    0x07));
            splits.Add(new WFSplit("VordtTeleport",       settings, worldFlagPointer, 0x11C7,   0x01, false));
            splits.Add(new WFSplit("Dancer",              settings, worldFlagPointer, 0xF6C,    0x05, false));
            splits.Add(new WFSplit("DancerLadder",        settings, worldFlagPointer, 0xF6D,    0x02, false));
            splits.Add(new WFSplit("Sage",                settings, worldFlagPointer, 0x2D69,   0x05, false));
            splits.Add(new WFSplit("Watchers",            settings, worldFlagPointer, 0x2D67,   0x07));
            splits.Add(new WFSplit("WatchersStairs",      settings, worldFlagPointer, 0x2FB7,   0x02, false));
            splits.Add(new WFSplit("Wolnir",              settings, worldFlagPointer, 0x5067,   0x07));
            splits.Add(new WFSplit("WolnirTeleport",      settings, worldFlagPointer, 0x50E7,   0x07, false));
            splits.Add(new WFSplit("ODK",                 settings, worldFlagPointer, 0x5064,   0x01));
            splits.Add(new WFSplit("Deacons",             settings, worldFlagPointer, 0x3C67,   0x07));
            splits.Add(new WFSplit("NamelessKing",        settings, worldFlagPointer, 0x2369,   0x05));
            splits.Add(new WFSplit("Pontiff",             settings, worldFlagPointer, 0x4B69,   0x05, false));
            splits.Add(new WFSplit("Aldrich",             settings, worldFlagPointer, 0x4B67,   0x07));
            splits.Add(new WFSplit("AldrichTeleport",     settings, worldFlagPointer, 0x4B03,   0x05, false));
            splits.Add(new WFSplit("Yhorm",               settings, worldFlagPointer, 0x5567,   0x07));
            splits.Add(new WFSplit("YhormTeleport",       settings, worldFlagPointer, 0x5503,   0x06, false));
            splits.Add(new WFSplit("Dragonslayer",        settings, worldFlagPointer, 0x1467,   0x07));
            splits.Add(new WFSplit("Oceiros",             settings, worldFlagPointer, 0xF64,    0x01));
            splits.Add(new WFSplit("Wyvern",              settings, worldFlagPointer, 0x2367,   0x07, false));
            splits.Add(new WFSplit("WyvernTeleport",      settings, worldFlagPointer, 0x2366,   0x04, false));
            splits.Add(new WFSplit("ChampionGundyr",      settings, worldFlagPointer, 0x5A64,   0x01));
            splits.Add(new WFSplit("TwinPrinces",         settings, worldFlagPointer, 0x3764,   0x01));
            splits.Add(new WFSplit("SoulOfCinder",        settings, worldFlagPointer, 0x5F67,   0x07));
            splits.Add(new WFSplit("Gravetender",         settings, worldFlagPointer, 0x6468,   0x03));
            splits.Add(new WFSplit("Friede",              settings, worldFlagPointer, 0x6467,   0x07));
            //DLC2
            splits.Add(new WFSplit("DemonPrinces",        settings, worldFlagPointer, 0x75E7, 0x07, false));
            splits.Add(new WFSplit("DemonPrincesBannerPickup",   settings, worldFlagPointer, 0x7AA5, 0x02, false));
            splits.Add(new WFSplit("DemonPrincesTeleport",       settings, worldFlagPointer, 0x7AA5, 0x01, false));
            splits.Add(new WFSplit("MidirCliff",          settings, worldFlagPointer, 0x7ABC, 0x06, false));
            splits.Add(new WFSplit("Midir",               settings, worldFlagPointer, 0x7AE9, 0x05));
            splits.Add(new WFSplit("Halflight",           settings, worldFlagPointer, 0x786C, 0x00));
            splits.Add(new WFSplit("GaelTeleport",        settings, worldFlagPointer, 0x7F9A, 0x06, false));
            splits.Add(new WFSplit("Gael",                settings, worldFlagPointer, 0x7FE7, 0x07));
            //misc splits
            splits.Add(new WFSplit("VilhelmStairs",       settings, worldFlagPointer, 0x644F,   0x05));
            //BBSplits
            splits.Add(new BBSplit("CatacombEntrance",  settings, new BoundingBox(366, -507, -257, 372, -501, -251)));
            splits.Add(new BBSplit("PontiffExit",       settings, new BoundingBox(397, -1211, -223, 403, -1205.5f, -220)));
            splits.Add(new BBSplit("SageExit",          settings, new BoundingBox(-204, -450, -248 , -199, -441, -238)));
            splits.Add(new BBSplit("HalflightElevator", settings, new BoundingBox(-392, -268, -52, -389, -265, -45)));
            //upgrade
            for(int i = 1; i < 10; i++)
            {
                splits.Add(new UpgradeSplit("Plus" + i, settings, maxUpgradePtr, i));
            }
        }

        public void process(Process dsProcess, TimerModel timer, out bool loadSplitQueued, out bool finalSplitFlag)
        {

            loadSplitQueued = false;
            finalSplitFlag = false;
            if (!initilized)
            {
                foreach (ISplit spl in splits)
                {
                    spl.init(dsProcess);
                }
                initilized = true;
            }
            else
            {
                foreach (ISplit spl in splits)
                {
                    if (spl.split(dsProcess, timer, _playerPos, out loadSplitQueued, out finalSplitFlag))
                        break;
                }
            }

        }
    }
}
