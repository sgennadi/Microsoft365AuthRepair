using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Microsoft365AuthRepair;

internal static class Native
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenUser = 1;
    private const uint CredentialGeneric = 1;
    private const int ErrorNotFound = 1168;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime { public uint Low; public uint High; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public nint TargetName;
        public nint Comment;
        public FileTime LastWritten;
        public uint CredentialBlobSize;
        public nint CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public nint Attributes;
        public nint TargetAlias;
        public nint UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredEnumerate(string? filter, uint flags, out uint count, out nint credentials);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(nint pointer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(nint process, uint desiredAccess, out nint token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(nint token, int informationClass, nint buffer, int bufferSize, out int returnLength);

    [DllImport("advapi32.dll", EntryPoint = "ConvertSidToStringSidW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertSidToStringSid(nint sid, out nint sidString);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint LocalFree(nint memory);

    private static readonly string[] CredentialParts =
    [
        "microsoft", "office", "adal", "oneauth", "msoid", "sso_pop", "tokenbroker"
    ];

    internal static int DeleteOfficeGenericCredentials()
    {
        if (!CredEnumerate(null, 0, out var count, out var list))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound) return 0;
            throw new Win32Exception(error, "Credential Manager enumeration failed");
        }
        var removed = 0;
        var failed = 0;
        try
        {
            for (var i = 0; i < count; i++)
            {
                var address = Marshal.ReadIntPtr(list, checked((int)i * IntPtr.Size));
                var credential = Marshal.PtrToStructure<Credential>(address);
                if (credential.Type != CredentialGeneric || credential.TargetName == 0) continue;
                var target = Marshal.PtrToStringUni(credential.TargetName);
                if (string.IsNullOrWhiteSpace(target)) continue;
                // Do not touch domain, Windows sign-in, or RDP/TERMSRV credentials.
                if (target.StartsWith("TERMSRV/", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("TERMSRV\\", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("Domain:target=", StringComparison.OrdinalIgnoreCase)) continue;
                if (!CredentialParts.Any(part => target.Contains(part, StringComparison.OrdinalIgnoreCase))) continue;
                if (CredDelete(target, credential.Type, 0)) removed++;
                else failed++;
            }
        }
        finally
        {
            CredFree(list);
        }
        if (failed > 0) throw new IOException($"Credential Manager failed to delete {failed} matching entries; {removed} removed.");
        return removed;
    }

    internal static string? ExplorerOwnerSid(int processId)
    {
        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == 0) return null;
        try
        {
            if (!OpenProcessToken(process, TokenQuery, out var token)) return null;
            try
            {
                GetTokenInformation(token, TokenUser, 0, 0, out var requiredSize);
                if (requiredSize < IntPtr.Size || requiredSize > 65536) return null;
                var buffer = Marshal.AllocHGlobal(requiredSize);
                try
                {
                    if (!GetTokenInformation(token, TokenUser, buffer, requiredSize, out _)) return null;
                    var pointer = Marshal.ReadIntPtr(buffer);
                    if (!ConvertSidToStringSid(pointer, out var sidString)) return null;
                    try { return Marshal.PtrToStringUni(sidString); }
                    finally { LocalFree(sidString); }
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            finally { CloseHandle(token); }
        }
        finally { CloseHandle(process); }
    }

    internal static Dictionary<int, HashSet<string>> FindExplorerSessions()
    {
        var sessions = new Dictionary<int, HashSet<string>>();
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            using (process)
            {
                try
                {
                    var sid = ExplorerOwnerSid(process.Id);
                    if (sid is null || !Regex.IsMatch(sid, @"^S-1-5-21-(?:\d+-){3}\d+$")) continue;
                    if (!sessions.TryGetValue(process.SessionId, out var owners))
                    {
                        owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        sessions.Add(process.SessionId, owners);
                    }
                    owners.Add(sid);
                }
                catch { /* Protected processes or session changes: skip. */ }
            }
        }
        return sessions;
    }
}
