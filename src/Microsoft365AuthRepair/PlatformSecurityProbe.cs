using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Microsoft365AuthRepair;

// Windows platform checks only. Do not retain OEM model text, serial numbers,
// BIOS firmware identifiers, TPM endorsement details or machine identity.
// A local Hyper-V/VBS hypervisor DOES NOT mean Windows is a VM guest.
internal static class PlatformSecurityProbe
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFirmwareType(out uint firmwareType);

    // Returns only UEFI/BIOS and enabled/disabled categories. Registry is
    // a read-only fallback supported by Windows Secure Boot inventory scripts.
    internal static (string Boot, string Firmware) ReadSecureBoot()
    {
        uint type;
        try
        {
            if (!GetFirmwareType(out type)) return ("UNKNOWN", "UNKNOWN");
        }
        catch (Exception ex) when (ex is DllNotFoundException or
            EntryPointNotFoundException or BadImageFormatException)
        { return ("UNKNOWN", "UNKNOWN"); }

        if (type == 1u) return ("UNSUPPORTED", "BIOS");
        if (type != 2u) return ("UNKNOWN", "UNKNOWN");
        try
        {
            using var state = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\SecureBoot\State", writable: false);
            var value = state?.GetValue("UEFISecureBootEnabled");
            return value switch
            {
                int n when n == 1 => ("ON", "UEFI"),
                int n when n == 0 => ("OFF", "UEFI"),
                _ => ("UNKNOWN", "UEFI")
            };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or
            System.Security.SecurityException or IOException or ArgumentException)
        { return ("UNKNOWN", "UEFI"); }
    }

    // Return an allowlisted provider only, never the BIOS/SMBIOS text.
    // "NO" means the detected OEM identifies as physical; it cannot prove
    // that virtualization has not been disguised in firmware.
    internal static (string Guest, string Platform) ClassifyGuest(
        string? manufacturer, string? model)
    {
        var maker = (manufacturer ?? "").Trim().ToUpperInvariant();
        var machine = (model ?? "").Trim().ToUpperInvariant();
        if (maker.Length == 0 && machine.Length == 0) return ("UNKNOWN", "UNKNOWN");

        if (maker.Contains("VMWARE", StringComparison.Ordinal) ||
            machine.Contains("VMWARE", StringComparison.Ordinal))
            return ("YES", "VMWARE");

        if (machine.Contains("VIRTUALBOX", StringComparison.Ordinal) ||
            maker.Contains("INNOTEK", StringComparison.Ordinal))
            return ("YES", "VIRTUALBOX");

        if (maker.Contains("AMAZON EC2", StringComparison.Ordinal) ||
            maker.Contains("AMAZON.COM", StringComparison.Ordinal))
            return ("YES", "AWS_EC2");

        if (machine.Contains("GOOGLE COMPUTE ENGINE", StringComparison.Ordinal) ||
            maker == "GOOGLE")
            return ("YES", "GOOGLE_CLOUD");

        if (maker.Contains("NUTANIX", StringComparison.Ordinal) ||
            machine.Contains("NUTANIX", StringComparison.Ordinal))
            return ("YES", "NUTANIX");

        if (machine.Contains("QEMU", StringComparison.Ordinal) ||
            machine.Contains("KVM", StringComparison.Ordinal) ||
            maker.Contains("QEMU", StringComparison.Ordinal) ||
            maker.Contains("KVM", StringComparison.Ordinal))
            return ("YES", "KVM_QEMU");

        if (machine.Contains("XEN", StringComparison.Ordinal) ||
            maker.Contains("XEN", StringComparison.Ordinal))
            return ("YES", "XEN");

        if (maker.Contains("PARALLELS", StringComparison.Ordinal) ||
            machine.Contains("PARALLELS", StringComparison.Ordinal))
            return ("YES", "PARALLELS");

        if ((maker.Contains("MICROSOFT", StringComparison.Ordinal) &&
             machine.Contains("VIRTUAL MACHINE", StringComparison.Ordinal)) ||
            machine.Contains("HYPER-V VIRTUAL", StringComparison.Ordinal))
            return ("YES", "HYPER_V");

        if (machine.Contains("VIRTUAL MACHINE", StringComparison.Ordinal) ||
            machine.Contains("VIRTUAL PLATFORM", StringComparison.Ordinal) ||
            maker.Contains("VIRTUAL", StringComparison.Ordinal))
            return ("YES", "OTHER_VM");

        if (new[] { "DELL", "LENOVO", "HEWLETT-PACKARD", "HP", "ASUSTEK",
            "ACER", "MICRO-STAR", "GIGABYTE", "FUJITSU", "SAMSUNG",
            "INTEL", "APPLE", "MICROSOFT CORPORATION" }
            .Any(oem => maker.Equals(oem, StringComparison.Ordinal) ||
                maker.StartsWith(oem + " ", StringComparison.Ordinal) ||
                maker.StartsWith(oem + ",", StringComparison.Ordinal)))
            return ("NO", "NONE");

        return ("UNKNOWN", "UNKNOWN");
    }

    internal static (string Guest, string Platform) ReadGuest()
    {
        try
        {
            using var search = new ManagementObjectSearcher(
                @"root\CIMV2",
                "SELECT Manufacturer, Model FROM Win32_ComputerSystem");
            using var result = search.Get();
            foreach (ManagementObject item in result)
            {
                using (item)
                    return ClassifyGuest(item["Manufacturer"] as string,
                        item["Model"] as string);
            }
        }
        catch (Exception ex) when (ex is ManagementException or
            System.Runtime.InteropServices.COMException or
            UnauthorizedAccessException or System.Security.SecurityException or
            InvalidOperationException or PlatformNotSupportedException)
        { }
        return ("UNKNOWN", "UNKNOWN");
    }

    internal static bool SelfTest()
    {
        return ClassifyGuest("VMware, Inc.", "VMware Virtual Platform") ==
                    ("YES", "VMWARE") &&
               ClassifyGuest("Microsoft Corporation", "Virtual Machine") ==
                    ("YES", "HYPER_V") &&
               ClassifyGuest("Microsoft Corporation", "Surface Laptop 6") ==
                    ("NO", "NONE") &&
               ClassifyGuest("innotek GmbH", "VirtualBox") ==
                    ("YES", "VIRTUALBOX") &&
               ClassifyGuest("QEMU", "Standard PC (Q35 + ICH9, 2009)") ==
                    ("YES", "KVM_QEMU") &&
               ClassifyGuest("Amazon EC2", "HVM domU") ==
                    ("YES", "AWS_EC2") &&
               ClassifyGuest("Xen", "HVM domU") ==
                    ("YES", "XEN") &&
               ClassifyGuest("Dell Inc.", "Latitude 7450") ==
                    ("NO", "NONE") &&
               ClassifyGuest("", "") == ("UNKNOWN", "UNKNOWN") &&
               ClassifyGuest("Unknown OEM", "Unknown laptop") ==
                    ("UNKNOWN", "UNKNOWN");
    }
}
