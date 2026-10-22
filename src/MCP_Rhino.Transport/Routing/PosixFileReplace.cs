using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MCP_Rhino.Transport.Routing;

// A plain Win32 replace fails while any reader has the destination open, even with delete
// sharing. A POSIX-semantics rename (Windows 10 1607+, NTFS) unlinks the old file instead, so
// readers that share delete keep their handle and never block the writer.
internal static class PosixFileReplace
{
    private const uint Delete = 0x00010000;
    private const uint Synchronize = 0x00100000;
    private const uint ShareAll = 0x00000007;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x80;
    private const int FileRenameInfoExClass = 22;
    private const uint ReplaceIfExists = 0x1;
    private const uint PosixSemantics = 0x2;
    private const int ErrorInvalidFunction = 1;
    private const int ErrorNotSupported = 50;
    private const int ErrorInvalidParameter = 87;

    // Returns false when the platform or file system does not support it; throws IOException when
    // the replace was attempted and refused.
    public static bool TryReplace(string source, string destination)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
        {
            return false;
        }

        using SafeFileHandle handle = CreateFileW(source, Delete | Synchronize, ShareAll, IntPtr.Zero, OpenExisting, FileAttributeNormal, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            throw ToIOException(Marshal.GetLastWin32Error());
        }

        string target = Path.GetFullPath(destination);
        int nameOffset = Marshal.OffsetOf<FileRenameInfoEx>(nameof(FileRenameInfoEx.FileName)).ToInt32();
        int size = Math.Max(nameOffset + ((target.Length + 1) * sizeof(char)), Marshal.SizeOf<FileRenameInfoEx>());
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.Copy(new byte[size], 0, buffer, size);
            Marshal.WriteInt32(buffer, Marshal.OffsetOf<FileRenameInfoEx>(nameof(FileRenameInfoEx.Flags)).ToInt32(), unchecked((int)(ReplaceIfExists | PosixSemantics)));
            Marshal.WriteIntPtr(buffer, Marshal.OffsetOf<FileRenameInfoEx>(nameof(FileRenameInfoEx.RootDirectory)).ToInt32(), IntPtr.Zero);
            Marshal.WriteInt32(buffer, Marshal.OffsetOf<FileRenameInfoEx>(nameof(FileRenameInfoEx.FileNameLength)).ToInt32(), target.Length * sizeof(char));
            Marshal.Copy(target.ToCharArray(), 0, buffer + nameOffset, target.Length);

            if (SetFileInformationByHandle(handle, FileRenameInfoExClass, buffer, (uint)size))
            {
                return true;
            }

            int error = Marshal.GetLastWin32Error();
            if (error is ErrorInvalidParameter or ErrorNotSupported or ErrorInvalidFunction)
            {
                return false;
            }

            throw ToIOException(error);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IOException ToIOException(int error)
    {
        return new IOException(new Win32Exception(error).Message, unchecked((int)0x80070000 | error));
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileRenameInfoEx
    {
        public uint Flags;
        public IntPtr RootDirectory;
        public uint FileNameLength;
        public char FileName;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle file,
        int fileInformationClass,
        IntPtr fileInformation,
        uint bufferSize);
}
