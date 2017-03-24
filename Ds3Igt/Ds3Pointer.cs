using System;
using System.Diagnostics;
using LiveSplit.ComponentUtil;

namespace Ds3Igt {
    class Ds3Pointer {

        private long _address;
        private IntPtr _dsHandle = IntPtr.Zero;
        private Process _dsProcess;
        private DeepPointer _dp;

        public long GetAddress() {
            return _address;
        }

        public Ds3Pointer() {
            GetProcess(Config.Module);
            FindAddress();
        }

        private void FindAddress() {
            if (_dsProcess == null) return;
            _dsHandle = _dsProcess.Id.GetHandle();
            string fileVer = _dsProcess.MainModule.FileVersionInfo.FileVersion;
            switch (fileVer) {
                case "1.3.1.0":
                    _address = Config.BasePointer1310.GetAddress(Config.Offsets1310[0], _dsHandle, 8);
                    break;
                case "1.3.2.0":
                    _address = Config.BasePointer1320.GetAddress(Config.Offsets1320[0], _dsHandle, 8);
                    break;
                case "1.3.2.1":
                    _address = Config.BasePointer1321.GetAddress(Config.Offsets1321[0], _dsHandle, 8);
                    break;
                case "1.4.1.0":
                case "1.4.3.0":
                    _address = Config.BasePointer1410.GetAddress(Config.Offsets1410[0], _dsHandle, 8);
                    break;
                case "1.5.0.0":
                    _address = Config.BasePointer1500.GetAddress(Config.Offsets1500[0], _dsHandle, 8);
                    break;
                case "1.5.1.0":
                    _address = Config.BasePointer1510.GetAddress(Config.Offsets1510[0], _dsHandle, 8);
                    break;
                case "1.6.0.0":
                    _address = Config.BasePointer1600.GetAddress(Config.Offsets1600[0], _dsHandle, 8);
                    break;
                case "1.7.0.0":
                    _address = Config.BasePointer1700.GetAddress(Config.Offsets1700[0], _dsHandle, 8);
                    break;
                case "1.8.0.0":
                    _address = Config.BasePointer1800.GetAddress(Config.Offsets1800[0], _dsHandle, 8);
                    break;
                case "1.9.0.0":
                    _address = Config.BasePointer1900.GetAddress(Config.Offsets1900[0], _dsHandle, 8);
                    break;
                case "1.11.0.0":
                default:
                    _address = Config.BasePointer11100.GetAddress(Config.Offsets11100[0], _dsHandle, 8);
                    break;
            }
        }

        private void GetProcess(string name) {
            Process[] processes = Process.GetProcessesByName(name);

            if (processes.Length <= 0) return;
            _dsProcess = processes[0];
            _dsProcess.EnableRaisingEvents = true;
            _dsProcess.Exited += (sender, args) => DoReset();
        }

        private void DoReset() {
            _dsProcess = null;
            _dsHandle = IntPtr.Zero;
            _address = 0;

        }

        public int GetIgt() {
            if (_dsProcess == null) {
                GetProcess(Config.Module);
                return 0;
            }

            if (_dsHandle == IntPtr.Zero) FindAddress();

            if (_address == 0) FindAddress();

            int millis = Native.GetGameTimeMilliseconds(_address, _dsHandle, 8);

            if (millis < 200)
                FindAddress();

            return Native.GetGameTimeMilliseconds(_address, _dsHandle, 8);
        }
    }
}
