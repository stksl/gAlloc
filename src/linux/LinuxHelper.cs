using System.Runtime.InteropServices;

internal static class LinuxHelper
{
    [Flags]
    public enum LinuxProt
    {
        PROT_READ = 0x1,
        PROT_WRITE = 0x2,
        PROT_EXEC = 0x4,
        PROT_NONE = 0x0,
    }
    [Flags]
    public enum LinuxFlags
    {
        MAP_SHARED = 0x01,
        MAP_PRIVATE = 0x02,
        MAP_ANONYMOUS = 0x20,
        MAP_NORESERVE = 0x04000,
        MAP_FIXED =0x10,
        
        MREMAP_MAYMOVE = 0x1,
        MREMAP_FIXED = 0x2
    }
    [DllImport("libc.so.6", SetLastError = true)]
    
    public static extern IntPtr mmap(IntPtr addr, int length, LinuxProt prot, LinuxFlags flags, int fd, long offset);
    [DllImport("libc.so.6", SetLastError = true)]
    public static extern IntPtr mremap(IntPtr old, int old_size, int new_size, LinuxFlags flags, IntPtr new_addr);
    [DllImport("libc.so.6", SetLastError = true)]
    public static extern int munmap(IntPtr addr, int length);
    [DllImport("libc.so.6")]
    public static extern int errno();
}