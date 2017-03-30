using System;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiveSplit.ComponentUtil;
using LiveSplit.Model;


namespace Ds3Igt
{
    abstract class Injection
    {
        protected Process _dsProcess;
        protected bool _injected;
        protected int _overrideLength;
        protected byte[] _overwrittenBytes;
        protected IntPtr _detourPtr;
        protected IntPtr _detourFuncBodyPtr;
        protected IntPtr _baseAddress;

        abstract protected bool Inject();

        protected bool RemoveInjection()
        {
            if (!_injected)
                return true;
            try
            {
                _dsProcess.WriteBytes(_detourPtr, _overwrittenBytes);
                _dsProcess.FreeMemory(_detourFuncBodyPtr);
                _injected = false;
                Trace.WriteLine("Remove Injection: success");
            }
            catch
            {
                Trace.WriteLine("Remove Injection: failed");
                return false;
            }

            return true;
        }

        protected void init(Process dsProcess)
        {
            _dsProcess = dsProcess;
            _injected = false;
            _overwrittenBytes = null;
            _detourFuncBodyPtr = IntPtr.Zero;
            _detourPtr = IntPtr.Zero;
            _baseAddress = IntPtr.Zero;
        }
    }

    class WorldFlagInjection : Injection
    {
        public WorldFlagInjection(Process dsProcess)
        {
            _overrideLength = 15;
            init(dsProcess);
        }

        ~WorldFlagInjection()
        {
            RemoveInjection();
        }

        public IntPtr getWorldFlagBaseAddress()
        {
            if (_baseAddress != IntPtr.Zero)
                return _baseAddress;

            Inject();
            _baseAddress = _dsProcess.ReadPointer(_detourFuncBodyPtr + 0x38);
            _baseAddress = _dsProcess.ReadPointer(_baseAddress);
            if (_baseAddress != IntPtr.Zero)
                RemoveInjection();
            Trace.WriteLine("worldFlagPtr: " + _baseAddress);
            return _baseAddress;
        }

        override protected bool Inject()
        {
            if (_injected)
                return true;

            var scanTarget = new SigScanTarget(0,
                "40 57",                    //  push rdi
                "41 56",                    //  push r14
                "48 83 EC 28",              //  rsp, 28
                "80 B9 ?? ?? ?? ?? 00",     //  cmp byte ptr[rcx + 00000228], 00
                "45 0F B6 F0"               //  movzx r14d, r8l
            );
            var scanner = new SignatureScanner(_dsProcess, _dsProcess.Modules[0].BaseAddress, _dsProcess.Modules[0].ModuleMemorySize);

            try
            {
                //allocate mem for detour function body
                if ((_detourFuncBodyPtr = _dsProcess.AllocateMemory(1000)) == IntPtr.Zero)
                    throw new Win32Exception();

                //scan for injection point
                _detourPtr = scanner.Scan(scanTarget);
                if (_detourPtr == IntPtr.Zero)
                    throw new Win32Exception();

                //read bytes from instruction point.
                _overwrittenBytes = _dsProcess.ReadBytes(_detourPtr, _overrideLength);
                if (_overwrittenBytes == null)
                    throw new Win32Exception();

                //build detour function body
                var detourFuncBodyBytes = new List<byte>() { };
                detourFuncBodyBytes.AddRange(_overwrittenBytes);
                detourFuncBodyBytes.AddRange(new byte[] { 0x48, 0x89, 0x0D, 0x22, 0x00, 0x00, 0x00, 0xFF, 0x25, 0x00, 0x00, 0x00, 0x00 });
                byte[] returnAddressBytes = BitConverter.GetBytes(((long)(_detourPtr + 0x0E)));
                detourFuncBodyBytes.AddRange(returnAddressBytes);

                //build detour
                var detourBytes = new List<byte>() { 0xFF, 0x25, 0x00, 0x00, 0x00, 0x00 };
                detourBytes.AddRange(BitConverter.GetBytes(((long)_detourFuncBodyPtr)));
                detourBytes.AddRange(new byte[] { 0x90 });

                //write memory.
                _dsProcess.Suspend();
                //write injection function
                if (!_dsProcess.WriteBytes(_detourFuncBodyPtr, detourFuncBodyBytes.ToArray()))
                    throw new Win32Exception();
                //write detour
                if (!_dsProcess.WriteBytes(_detourPtr, detourBytes.ToArray()))
                    throw new Win32Exception();
                _dsProcess.Resume();
            }
            catch
            {
                _dsProcess.FreeMemory(_detourFuncBodyPtr);
                _dsProcess.Resume();
                return false;
            }
            _injected = true;
            Trace.WriteLine("World Flag Injection: success");
            Thread.Sleep(100); //wait a bit for the process to pass through the injection
            return true;
        }
    }


    //Getting Timer Address via injection
    //140591BF2 - 0F28 C6                   - movaps xmm0, xmm6
    //140591BF5 - F3 0F59 05 E3AA7A03       - mulss xmm0,[143D3C6E0]
    //140591BFD - F3 48 0F2C C0             - cvttss2si rax,xmm0
    //140591C02 - 01 81 A4000000            - add [rcx+000000A4],eax
    //140591C08 - 48 8B 05 59261704         - mov rax,[144704268] 
    //~~~~~~~~~Injection Point
    //140591C0F - 81 B8 A4000000 18A093D6   - cmp[rax + 000000A4],D693A018 //Incrementing and decrementing right after if the timer is to high....From Software please.
    //140591C19 - 76 0A                     - jna 140591C25
    //140591C1B - C7 80 A4000000 18A093D6   - mov[rax + 000000A4],D693A018
    //~~~~~~~~~Injection Return
    //140591C25 - 0F28 C6                   - movaps xmm0, xmm6

    class TimerInjection : Injection
    {
        private int _oldTime;
        private int _time;

        public TimerInjection(Process dsProcess)
        {
            _overrideLength = 22;
            init(dsProcess);
        }

        ~TimerInjection()
        {
            RemoveInjection();
        }

        public int getTime()
        {
            if (_baseAddress != IntPtr.Zero)
            {
                _oldTime = _time;
                _time = _dsProcess.ReadValue<int>(_baseAddress);
                return _time;
            }

            Inject();
            _baseAddress = _dsProcess.ReadPointer(_detourFuncBodyPtr + 0x41);
            if (_baseAddress != IntPtr.Zero)
                RemoveInjection();

            return 0;
        }

        public int getOldTime()
        {
            return _oldTime;
        }

        override protected bool Inject()
        {
            if (_injected)
                return true;

            //this injection point seems a bit fucked up. It gets rewritten when the player enters certain locations. Shouldn't be a problem though.
            var scanTarget = new SigScanTarget(0,
                "0F 28 C6",                 //  movaps xmm0, xmm6
                "F3 0F 59 05 ?? ?? ?? ??",  //  mulss xmm0, [????????]
                "F3 48 0F 2C C0",           //  cvttss2si rax, eax
                "01 81"                     //  add [timerPtr], eax
            );
            var scanner = new SignatureScanner(_dsProcess, _dsProcess.Modules[0].BaseAddress, _dsProcess.Modules[0].ModuleMemorySize);

            try
            {
                //allocate mem for detour function body
                if ((_detourFuncBodyPtr = _dsProcess.AllocateMemory(1000)) == IntPtr.Zero)
                    throw new Win32Exception();

                //scan for injection point
                _detourPtr = scanner.Scan(scanTarget);
                if (_detourPtr == IntPtr.Zero)
                    throw new Win32Exception();
                _detourPtr = _detourPtr + 0x1D; //inject 0x1D bytes after scanner target

                //read bytes from instruction point.
                _overwrittenBytes = _dsProcess.ReadBytes(_detourPtr, _overrideLength);
                if (_overwrittenBytes == null)
                    throw new Win32Exception();
                //build detour function body
                var detourFuncBodyBytes = new List<byte>() { };
                detourFuncBodyBytes.AddRange(_overwrittenBytes);
                detourFuncBodyBytes.AddRange(new byte[] { 0x41, 0x56, 0x4C, 0x8B, 0xF0, 0x49, 0x81, 0xC6 });
                detourFuncBodyBytes.AddRange(_overwrittenBytes.ToList().GetRange(2, 4).ToArray());
                detourFuncBodyBytes.AddRange(new byte[] { 0x4C, 0x89, 0x34, 0x25 });
                detourFuncBodyBytes.AddRange(BitConverter.GetBytes((int)(_detourFuncBodyPtr + 0x41)));
                detourFuncBodyBytes.AddRange(new byte[] { 0x41, 0x5E });
                detourFuncBodyBytes.AddRange(new byte[] { 0xFF, 0x25, 0x00, 0x00, 0x00, 0x00 });
                detourFuncBodyBytes.AddRange(BitConverter.GetBytes((long)(_detourPtr + 0x16)));

                //build detour
                var detourBytes = new List<byte>() { 0xFF, 0x25, 0x00, 0x00, 0x00, 0x00 };
                detourBytes.AddRange(BitConverter.GetBytes(((long)_detourFuncBodyPtr)));
                detourBytes.AddRange(new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 });
                //write memory.
                _dsProcess.Suspend();
                //write injection function
                if (!_dsProcess.WriteBytes(_detourFuncBodyPtr, detourFuncBodyBytes.ToArray()))
                    throw new Win32Exception();
                //write detour
                if (!_dsProcess.WriteBytes(_detourPtr, detourBytes.ToArray()))
                    throw new Win32Exception();
                _dsProcess.Resume();
            }
            catch
            {
                _dsProcess.FreeMemory(_detourFuncBodyPtr);
                _dsProcess.Resume();
                return false;
            }
            _injected = true;
            Trace.WriteLine("Timer Injection: success");
            Thread.Sleep(100); //wait a bit for the process to pass through the injection
            return true;
        }
    }

    //Injection to get printed Strings, for splitting at area change messages, item pickups etc.
    /*
    class StringPrintInjection : Injection
    {
        public StringPrintInjection(Process dsProcess)
        {
            _overrideLength = 14;
            init(dsProcess);
        }

        public int getAddress()
        {
            if (_injected == false)
                Inject();
            return 1;
        }

        override protected bool Inject()
            {
            if (_injected)
                return true;

                //find string buffer
                var scanTargetStringBuffer = new SigScanTarget(0,
                    "59 69 44 01 00 00 00 01 00 00 00 00 00 00 00 3C 54 45 58 54 46 4F 52 4D 41 54 20 4C 45 41 44 49 4E 47 3D 27 30 27 3E 3C 46 4F 4E 54 20 4C 45 54 54 45 52 53 50 41 43 49 4E 47 3D 27 30 27 3E"
                );
                var scannerStringBuffer = new SignatureScanner(_dsProcess, _dsProcess.Modules[0].BaseAddress, _dsProcess.Modules[0].ModuleMemorySize);
                var stringBufferAddress = scannerStringBuffer.Scan(scanTargetStringBuffer);
                if (stringBufferAddress == IntPtr.Zero)
                    throw new Win32Exception();
                stringBufferAddress = stringBufferAddress + 0x3F;

                //this injection point seems a bit fucked up. It gets rewritten when the player enters certain locations. Shouldn't be a problem though.
                var scanTarget = new SigScanTarget(0,
                "48 8B 74 24 40",       //  mov rsi,[rsp+??]
                "48 8B 5C 24 50",       //  mov rbi,[rsp+??]
                "C6 04 28 00",          //  mov byte ptr [rax+rbx],00
                "48 83 C4 20",          //  add rsp,20
                "41 5E"                 //  pop r14
            );
            var scanner = new SignatureScanner(_dsProcess, _dsProcess.Modules[0].BaseAddress, _dsProcess.Modules[0].ModuleMemorySize);

            try
            {
                //allocate mem for detour function body
                if ((_detourFuncBodyPtr = _dsProcess.AllocateMemory(1000)) == IntPtr.Zero)
                    throw new Win32Exception();
                Trace.WriteLine("String detourFuncBodyPtr: " + _detourFuncBodyPtr);

                //scan for injection point
                _detourPtr = scanner.Scan(scanTarget);
                if (_detourPtr == IntPtr.Zero)
                    throw new Win32Exception();

                //read bytes from instruction point.
                var _overwrittenBytes = _dsProcess.ReadBytes(_detourPtr, _overrideLength);
                if (_overwrittenBytes == null)
                    throw new Win32Exception();

                //build detour function body
                var detourFuncBodyBytes = new List<byte>() { };
                detourFuncBodyBytes.AddRange(new byte[] { 0x14, 0x56 }); //push r14
                detourFuncBodyBytes.AddRange(_overwrittenBytes);
                detourFuncBodyBytes.AddRange(new byte[] { 0x52, 0x51 }); //push rcx, push rdx
                detourFuncBodyBytes.AddRange(new byte[] { 0x48, 0xBA });
                detourFuncBodyBytes.AddRange(BitConverter.GetBytes((long)stringBufferAddress));
                detourFuncBodyBytes.AddRange(new byte[] { 0x48, 0x8B, 0x12 });

                //detourFuncBodyBytes.AddRange(new byte[] { 0x48, 0xB9, 0x43, 0x61, 0x74, 0x61, 0x63, 0xFF, 0x6D, 0x62 });    // compare to "dummy"
                //detourFuncBodyBytes.AddRange(new byte[] { 0x48, 0x39, 0xCA, 0x75, 0x0F });                                  // 

                detourFuncBodyBytes.AddRange(new byte[] { 0x48, 0xB9, 0x43, 0x61, 0x74, 0x61, 0x63, 0x6F, 0x6D, 0x62 });    // compare to "Catacomb"
                detourFuncBodyBytes.AddRange(new byte[] { 0x48, 0x39, 0xCA, 0x75, 0x07 });                                  // jmp 7 on las cmp.

                //4-7th byte are relative offset of variable pointer from variable write instruction
                detourFuncBodyBytes.AddRange(new byte[] {0x48, 0x89, 0x15, 0x26, 0x00, 0x00, 0x00, 0x59, 0x5A, 0xFF, 0x25, 0x00, 0x00, 0x00, 0x00 });
                detourFuncBodyBytes.AddRange(BitConverter.GetBytes(((long)_detourPtr + 0x0E)));

                //build detour
                var detourBytes = new List<byte>() { 0xFF, 0x25, 0x00, 0x00, 0x00, 0x00 };
                detourBytes.AddRange(BitConverter.GetBytes(((long)_detourFuncBodyPtr)));
                detourBytes.AddRange(new byte[] { });

                //write memory.
                _dsProcess.Suspend();
                //write injection function
                if (!_dsProcess.WriteBytes(_detourFuncBodyPtr, detourFuncBodyBytes.ToArray()))
                    throw new Win32Exception();
                //write detour
                if (!_dsProcess.WriteBytes(_detourPtr, detourBytes.ToArray()))
                    throw new Win32Exception();
                _dsProcess.Resume();
            }
            catch
            {
                _dsProcess.FreeMemory(_detourFuncBodyPtr);
                _dsProcess.Resume();
                throw;
            }
            _injected = true;
            Thread.Sleep(100); //wait a bit for the process to pass through the injection
            return true;
        }
    }
    */

}


