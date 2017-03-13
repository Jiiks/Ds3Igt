using System;
using System.Runtime.InteropServices;

namespace Ds3Igt {
    public static class Native {

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize,
                ref int lpNumberOfBytesRead);
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandler, int dwProcessId);
        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, long lpBaseAddress, byte[] lpBuffer, int dwSize,
                ref int lpNumberOfBytesRead);

        public static IntPtr GetHandle(this int pid) {
            return OpenProcess(0x0010, false, pid);
        }

        public static int GetAddress(this int baseAddress, int offset, IntPtr handle, int bufferSize) {
            int bytesRead = 0;
            byte[] buffer = new byte[bufferSize];

            ReadProcessMemory((int)handle, baseAddress, buffer, buffer.Length, ref bytesRead);

            return BitConverter.ToInt32(buffer, 0) + offset;
        }

        public static long GetAddress(this long baseAddress, int offset, IntPtr handle, int bufferSize) {
            int bytesRead = 0;
            byte[] buffer = new byte[bufferSize];

            ReadProcessMemory((int)handle, baseAddress, buffer, buffer.Length, ref bytesRead);

            return BitConverter.ToInt64(buffer, 0) + offset;
        }

        public static int GetGameTimeMilliseconds(int address, IntPtr handle, int bufferSize) {
            int bytesRead = 0;
            byte[] buffer = new byte[bufferSize];

            ReadProcessMemory((int)handle, address, buffer, buffer.Length, ref bytesRead);

            return BitConverter.ToInt32(buffer, 0);
        }

        public static int GetGameTimeMilliseconds(long address, IntPtr handle, int bufferSize) {
            int bytesRead = 0;
            byte[] buffer = new byte[bufferSize];

            ReadProcessMemory((int)handle, address, buffer, buffer.Length, ref bytesRead);

            return BitConverter.ToInt32(buffer, 0);
        }
    }
}