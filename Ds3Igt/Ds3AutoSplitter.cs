using System;
using System.Windows.Forms;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using LiveSplit.Model;
using LiveSplit.ComponentUtil;

namespace Ds3Igt
{
    class Ds3AutoSplitter : UserControl
    {
        private IntPtr _dsHandle;
        private Process _dsProcess;
        private int _splitAttemptCnt;
        private double _updateRateMuliplier; //default Component update rate is 60? We don't need this rate for splitting.
        private bool _loadSplitQueued;
        private TreeView _settings;
        private Ds3Splits _splits;
        private TimerModel _timer;
        private bool _initilized;

        //injections
        private TimerInjection _timerInj;
        private WorldFlagInjection _worldFlagInj;

        private bool GetProcess()
        {
            if (_dsProcess != null)
                return true;
            Process[] processesByName = Process.GetProcessesByName(Config.Module);
            if (processesByName.Length == 0)
                return false;
            _dsProcess = processesByName[0];
            _dsHandle = _dsProcess.Handle;
            _dsProcess.EnableRaisingEvents = true;
            _dsProcess.Exited += ((sender, args) => Reset());
            return true;
        }

        private void Reset()
        {
            _dsHandle = IntPtr.Zero;
            _dsProcess = null;
            _loadSplitQueued = false;
            _timerInj = null;
            _worldFlagInj = null;
            _splits = null;
            _initilized = false;
        }

        private void ResetSplits()
        {
            _loadSplitQueued = false;
            _initilized = false;
            _splits = null;
        }

        public Ds3AutoSplitter(LiveSplitState state, TreeView settings)
        {
            _dsHandle = IntPtr.Zero;
            _dsProcess = null;
            _splitAttemptCnt = 0;
            _updateRateMuliplier = 0.16;
            _loadSplitQueued = false;
            _timerInj = null;
            _worldFlagInj = null;
            _splits = null;
            _settings = settings;
            _initilized = false;
            _timer = new TimerModel();
            _timer.CurrentState = state;
            _timer.CurrentState.OnStart += ((sender, args) => ResetSplits());
        }

        private bool init()
        {
            if (_initilized)
                return true;

            if (!GetProcess())
                return false;

            if (_timerInj == null)
                _timerInj = new TimerInjection(_dsProcess);

            if (_worldFlagInj == null)
                _worldFlagInj = new WorldFlagInjection(_dsProcess);

            if (_splits == null)
            {
                IntPtr worldFlagPointer = _worldFlagInj.getWorldFlagBaseAddress();
                if(worldFlagPointer == IntPtr.Zero)
                    return false;
                _splits = new Ds3Splits(_dsProcess, worldFlagPointer, _settings);
            }

            _initilized = true;
            return true;
        }

        public void AttemptSplit()
        {
            if (!init())
                return;

            _splitAttemptCnt++;

            if (_splitAttemptCnt % (int)(1 / _updateRateMuliplier) == 0)
            {
                //Load split
                if (_loadSplitQueued)
                {
                    if (_timerInj.getOldTime() == _timerInj.getTime()) {
                        _timer.Split();
                        _loadSplitQueued = false;
                    }
                    return;
                }

                if (_timerInj.getTime() == 0)
                    return;

                _splits.process(_dsProcess, _timer, out _loadSplitQueued);
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // Ds3AutoSplitter
            // 
            this.Name = "Ds3AutoSplitter";
            this.Size = new System.Drawing.Size(199, 176);
            this.ResumeLayout(false);

        }
    }
}
