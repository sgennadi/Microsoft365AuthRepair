using Microsoft.Win32;
using System.Globalization;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Microsoft365AuthRepair;

// Office-only WMI inventory and explicitly confirmed key replacement.
// Never touches Windows licensing application IDs, scans full keys, or
// treats unverified "Office 2026 Preview" as a released Volume SKU.
internal sealed record OfficeVolumeProduct(
    string ActivationId, string Name, string Description, string ProductId,
    string PartialKey, string Status, bool IsPreview,
    bool IsSupported2024Volume, string? ConfiguredKms, int? ConfiguredPort,
    string? DiscoveredKms, int? DiscoveredPort)
{
    internal bool HasKey => PartialKey.Length == 5;
    internal bool CanChange => IsSupported2024Volume && HasKey;
}

internal sealed record OfficeKmsEndpoint(string Host, int Port, string Source);
internal sealed record OfficeVolumeInventory(IReadOnlyList<OfficeVolumeProduct> Products,
    IReadOnlyList<string> ProductReleaseIds, string? Warning);
internal sealed record OfficeLicenseChangeResult(bool Success, string Stage, string Status,
    bool PreviewKeyRemoved, bool ActivationVerified);

internal static class OfficeVolumeLicensing
{
    internal const string OfficeApplicationId = "0ff1ce15-a989-479d-af46-f275c6370663";
    internal const string GvlkSource = "https://learn.microsoft.com/en-us/office/volume-license-activation/gvlks";
    private const string ConfigKey = @"SOFTWARE\Microsoft\Office\ClickToRun\Configuration";
    private const string WmiPath = @"\\.\root\cimv2";

    // Exactly six Microsoft-published GVLKs for supported 2024 suites,
    // Visio and Project. No unverified 2026/preview key is substituted.
    private static readonly IReadOnlyDictionary<string, string> Gvlk =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProPlus2024Volume"] = "XJ2XN-FW8RK-P4HMP-DKDBV-GCVGB",
            ["Standard2024Volume"] = "V28N4-JG22K-W66P8-VTMGK-H6HGR",
            ["ProjectPro2024Volume"] = "FQQ23-N4YCY-73HQ3-FM9WC-76HF4",
            ["ProjectStd2024Volume"] = "PD3TT-NTHQQ-VC7CY-MFXK3-G87F8",
            ["VisioPro2024Volume"] = "B7TN8-FJ8V3-7QYCP-HQPMV-YY89G",
            ["VisioStd2024Volume"] = "JMMVY-XFNQC-KK4HK-9H7R3-WQQTV"
        };

    // Last five characters of Microsoft 2024 commercial-preview KMS keys
    // (Office/Project/Visio respectively). Partial keys are never secret.
    // Do NOT infer Preview status only from the Office build number.
    private static readonly IReadOnlyDictionary<string, string> PreviewSuffixes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProPlus2024Volume"] = "TX3T2",
            ["ProjectPro2024Volume"] = "JB89R",
            ["VisioPro2024Volume"] = "WXGKQ"
        };

    private static string? ReadConfigValue(string key)
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var cfg = hklm.OpenSubKey(ConfigKey);
                var value = cfg?.GetValue(key);
                if (value is not null) return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or
                System.Security.SecurityException or IOException) { }
        }
        return null;
    }

    private static string Str(ManagementBaseObject item, string field)
    {
        try { return Convert.ToString(item[field], CultureInfo.InvariantCulture)?.Trim() ?? ""; }
        catch (ManagementException) { return ""; }
    }

    private static int? Port(ManagementBaseObject item, string field)
    {
        var input = Str(item, field);
        return int.TryParse(input, out var port) && port is > 0 and <= 65535 ? port : null;
    }

    internal static string? DetectSku(string text)
    {
        foreach (var key in Gvlk.Keys.OrderByDescending(k => k.Length))
        {
            if (text.Contains(key, StringComparison.OrdinalIgnoreCase))
                return key;
            // WMI formats: Office24ProPlus2024VL_KMS_Client, etc.
            var baseSku = key.Replace("Volume", "", StringComparison.Ordinal);
            if (text.Contains(baseSku + "VL", StringComparison.OrdinalIgnoreCase))
                return key;
        }
        return null;
    }

    internal static bool IsPreviewLabel(string label) =>
        Regex.IsMatch(label, @"(?i)(?<![a-z])(?:preview|insider|evaluation|eval)(?![a-z])",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    internal static bool IsUnverified2026(string label) =>
        Regex.IsMatch(label, @"(?i)(?<!\d)2026(?!\d)",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    internal static bool IsKnownPreviewKey(string? sku, string? partial)
    {
        if (sku is null || partial is null) return false;
        return PreviewSuffixes.TryGetValue(sku, out var expected) &&
            expected.Equals(partial, StringComparison.OrdinalIgnoreCase);
    }

    // Exact 2024 Volume edition must be present. A previous Preview key does
    // not make a retail/Microsoft 365 subscription suitable for KMS activation.
    private static bool ProductReleaseMatches(string sku, IReadOnlyList<string> releaseIds) =>
        releaseIds.Any(x => x.Equals(sku, StringComparison.OrdinalIgnoreCase) ||
            x.Equals(sku + "-Preview", StringComparison.OrdinalIgnoreCase));

    private static string ProductStatus(string status) => status switch
    {
        "1" => "Licensed",
        "0" => "Unlicensed",
        "2" => "Initial grace",
        "3" => "Expired grace",
        "4" => "Non-genuine",
        "5" => "Notification",
        "6" => "Extended grace",
        _ => "Unknown"
    };

    internal static OfficeVolumeInventory Read()
    {
        var releases = (ReadConfigValue("ProductReleaseIds") ?? "")
            .Split(',', ';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(48).ToArray();
        var rows = new List<OfficeVolumeProduct>();
        string? warning = null;
        try
        {
            using var search = new ManagementObjectSearcher(WmiPath,
                "SELECT ID,Name,Description,ApplicationID,PartialProductKey,LicenseStatus," +
                "KeyManagementServiceMachine,KeyManagementServicePort," +
                "DiscoveredKeyManagementServiceMachineName,DiscoveredKeyManagementServiceMachinePort" +
                " FROM SoftwareLicensingProduct WHERE ApplicationID = '" + OfficeApplicationId + "'");
            using var results = search.Get();
            foreach (ManagementObject entry in results)
            {
                using (entry)
                {
                    var id = Str(entry, "ID");
                    if (!Guid.TryParse(id, out _)) continue;
                    var name = Str(entry, "Name");
                    var sku = DetectSku(name) ?? DetectSku(Str(entry, "Description")) ?? "";
                    var foundRelease = releases.FirstOrDefault(x =>
                        !string.IsNullOrEmpty(sku) &&
                        x.StartsWith(sku, StringComparison.OrdinalIgnoreCase) &&
                        (x.Length == sku.Length || x[sku.Length] == '-')) ?? "";
                    var combinedLabel = name + " " + Str(entry, "Description") + " " + foundRelease;
                    var partial = Str(entry, "PartialProductKey");
                    var knownPreviewKey = IsKnownPreviewKey(sku, partial);
                    // An exact published Preview GVLK suffix is a much more
                    // reliable identifier than a label containing "Preview".
                    rows.Add(new OfficeVolumeProduct(
                        id, name.Length > 0 ? name : "Unknown Office product",
                        Str(entry, "Description"), sku, partial.Length == 5 ? partial : "",
                        ProductStatus(Str(entry, "LicenseStatus")),
                        IsPreviewLabel(combinedLabel) || knownPreviewKey ||
                            IsUnverified2026(combinedLabel),
                        Gvlk.ContainsKey(sku) &&
                            foundRelease.Equals(sku, StringComparison.OrdinalIgnoreCase) &&
                            !IsUnverified2026(combinedLabel) &&
                            !IsPreviewLabel(combinedLabel) && !knownPreviewKey,
                        Str(entry, "KeyManagementServiceMachine") is { Length: > 0 } configured ? configured : null,
                        Port(entry, "KeyManagementServicePort"),
                        Str(entry, "DiscoveredKeyManagementServiceMachineName") is { Length: > 0 } discovered ? discovered : null,
                        Port(entry, "DiscoveredKeyManagementServiceMachinePort")));
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or
            System.Runtime.InteropServices.COMException or PlatformNotSupportedException)
        {
            warning = "Office licensing provider unavailable: " + ex.GetType().Name;
        }

        return new OfficeVolumeInventory(rows, releases, warning);
    }

    // For a new preview candidate, do not enable conversion merely because
    // another unrelated suite has an official 2024 Volume GVLK.
    internal static bool CanReplacePreview(OfficeVolumeProduct product, OfficeVolumeInventory inventory)
    {
        if (!product.IsPreview || !product.HasKey ||
            IsUnverified2026(product.Name + " " + product.Description))
            return false;
        var sku = DetectSku(product.Name + " " + product.Description);
        if (sku is null || !Gvlk.ContainsKey(sku) ||
            !ProductReleaseMatches(sku, inventory.ProductReleaseIds))
            return false;

        // Preview names are not consistent across old WMI/C2R builds.
        // Accept a well-known Preview KMS suffix, or an explicit "-Preview"
        // product release with a Preview-labeled product. Never guess from
        // merely having a non-GVLK partial key (it may be a valid MAK).
        return IsKnownPreviewKey(sku, product.PartialKey) ||
            IsPreviewLabel(product.Name + " " + product.Description) &&
            inventory.ProductReleaseIds.Contains(sku + "-Preview", StringComparer.OrdinalIgnoreCase);
    }

    internal static bool CanApply(OfficeVolumeProduct product, OfficeVolumeInventory inventory) =>
        product.CanChange || CanReplacePreview(product, inventory);

    // Preflight must be conservative: we will not remove the target product's
    // only key if its exact Preview identity is not independently verified.
    internal static bool ShouldRemoveOldKey(OfficeVolumeProduct product, OfficeVolumeInventory inventory) =>
        CanReplacePreview(product, inventory);

    internal static string? GvlkFor(OfficeVolumeProduct product) =>
        DetectSku(product.Name + " " + product.Description) is { } key &&
        Gvlk.TryGetValue(key, out var gvlk) ? gvlk : null;

    internal static string? ValidateHost(string candidate)
    {
        var host = candidate.Trim().TrimEnd('.');
        if (host.Length is < 1 or > 253 || host.Contains(' ') ||
            host.Any(char.IsControl) ||
            host.IndexOfAny(new[] { '/', '\\', '@', ':', ';', '?', '#' }) >= 0)
            return null;
        if (IPAddress.TryParse(host, out var ip))
            return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? host : null;
        return Uri.CheckHostName(host) == UriHostNameType.Dns ? host : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DnsSrvRecord
    {
        public IntPtr Next;
        public IntPtr Name;
        public ushort Type;
        public ushort DataLength;
        public uint Flags;
        public uint Ttl;
        public uint Reserved;
        public IntPtr Target;
        public ushort Priority;
        public ushort Weight;
        public ushort Port;
        public ushort Padding;
    }

    [DllImport("dnsapi.dll", CharSet = CharSet.Unicode, EntryPoint = "DnsQuery_W")]
    private static extern int QueryDns(string name, ushort type, uint options,
        IntPtr extra, out IntPtr results, IntPtr reserved);

    [DllImport("dnsapi.dll", EntryPoint = "DnsRecordListFree")]
    private static extern void FreeDnsRecords(IntPtr ptr, int freeType);

    internal static IReadOnlyList<OfficeKmsEndpoint> DiscoverKms(OfficeVolumeProduct product)
    {
        var endpoints = new List<OfficeKmsEndpoint>();
        void Add(string? hostname, int? port, string source)
        {
            if (hostname is null) return;
            var clean = ValidateHost(hostname);
            if (clean is null) return;
            var endpoint = new OfficeKmsEndpoint(clean, port is > 0 and < 65536 ? port.Value : 1688, source);
            if (!endpoints.Any(x => x.Host.Equals(clean, StringComparison.OrdinalIgnoreCase) &&
                x.Port == endpoint.Port))
                endpoints.Add(endpoint);
        }
        Add(product.ConfiguredKms, product.ConfiguredPort, "Office WMI: configured");
        Add(product.DiscoveredKms, product.DiscoveredPort, "Office WMI: DNS-discovered");

        // Windows DNS API SRV lookup; no PowerShell or external process.
        // This is a DNS query only, NOT activation or validation of a server.
        var suffixes = new List<string>();
        try
        {
            var domain = IPGlobalProperties.GetIPGlobalProperties().DomainName;
            if (!string.IsNullOrWhiteSpace(domain)) suffixes.Add(domain);
        }
        catch (NetworkInformationException) { }
        foreach (var suffix in suffixes.Distinct(StringComparer.OrdinalIgnoreCase).Take(2))
        {
            var query = "_vlmcs._tcp." + suffix.TrimEnd('.');
            IntPtr list = IntPtr.Zero;
            try
            {
                if (QueryDns(query, 33, 0, IntPtr.Zero, out list, IntPtr.Zero) != 0)
                    continue;
                var current = list;
                for (int i = 0; i < 16 && current != IntPtr.Zero; i++)
                {
                    var item = Marshal.PtrToStructure<DnsSrvRecord>(current);
                    if (item.Type == 33)
                    {
                        var target = Marshal.PtrToStringUni(item.Target);
                        Add(target, item.Port, "DNS SRV: " + query);
                    }
                    current = item.Next;
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or
                System.Runtime.InteropServices.COMException or ArgumentException) { }
            finally { if (list != IntPtr.Zero) FreeDnsRecords(list, 1); }
        }
        return endpoints;
    }

    private static ManagementObject LoadOfficeProduct(string activationId)
    {
        if (!Guid.TryParse(activationId, out var id))
            throw new InvalidOperationException("Invalid Office activation ID.");
        using var search = new ManagementObjectSearcher(WmiPath,
            "SELECT * FROM SoftwareLicensingProduct WHERE ID = '" +
            id.ToString("D") + "' AND ApplicationID = '" + OfficeApplicationId + "'");
        using var list = search.Get();
        foreach (ManagementObject item in list) return item;
        throw new InvalidOperationException("Office product could not be verified.");
    }

    private static int Invoke(ManagementObject instance, string method, string? parameter, object? value)
    {
        using var inputs = parameter is null ? null : instance.GetMethodParameters(method);
        if (inputs is not null) inputs[parameter!] = value;
        using var output = instance.InvokeMethod(method, inputs, null);
        if (output is null) throw new InvalidOperationException("Licensing provider returned no result.");
        var code = Convert.ToUInt32(output["ReturnValue"], CultureInfo.InvariantCulture);
        if (code != 0) throw new InvalidOperationException("Licensing error: 0x" + code.ToString("X8"));
        return 0;
    }

    internal static OfficeLicenseChangeResult ReplaceKey(
        OfficeVolumeProduct product, OfficeVolumeInventory oldInventory,
        bool mak, string? inputKey, OfficeKmsEndpoint? kms)
    {
        if (!Identity.IsElevated())
            return new(false, "Privileges", "Run as administrator and confirm the selected product.", false, false);
        if (!CanApply(product, oldInventory))
            return new(false, "Validation", "Unknown, subscription, 2026 or unsupported Preview products cannot be changed.", false, false);

        string? key = mak ? inputKey?.Trim().ToUpperInvariant() : GvlkFor(product);
        if (key is null || !Regex.IsMatch(key, @"^[A-Z0-9]{5}(-[A-Z0-9]{5}){4}$",
                RegexOptions.CultureInvariant))
            return new(false, "Validation", "Invalid or missing key format. No changes made.", false, false);
        if (!mak && kms is not null && (ValidateHost(kms.Host) is null || kms.Port is < 1 or > 65535))
            return new(false, "Validation", "Invalid KMS host/port. No changes made.", false, false);

        // Fresh pre-flight prevents a stale UI selection from removing a key
        // after a product was changed by another management system.
        var fresh = Read();
        var oldRow = fresh.Products.FirstOrDefault(x => x.ActivationId == product.ActivationId);
        if (oldRow is null || oldRow.PartialKey != product.PartialKey ||
            !CanApply(oldRow, fresh))
            return new(false, "Recheck", "Product changed since selection; refresh inventory.", false, false);
        bool removeOldPreview = CanReplacePreview(oldRow, fresh);
        bool oldRemoved = false;

        try
        {
            // The original Preview key must be removed before attempting to
            // activate the final 2024 license. Only the selected Office
            // ApplicationID + exact ActivationId + last-five may be removed.
            // This cannot be rolled back without the full old key. The UI
            // therefore requires a separate destructive consent beforehand.
            if (removeOldPreview)
            {
                using (var old = LoadOfficeProduct(oldRow.ActivationId))
                {
                    var directSku = DetectSku(Str(old, "Name") + " " + Str(old, "Description"));
                    if (!Str(old, "ApplicationID").Equals(OfficeApplicationId, StringComparison.OrdinalIgnoreCase) ||
                        !Str(old, "PartialProductKey").Equals(oldRow.PartialKey, StringComparison.OrdinalIgnoreCase) ||
                        directSku != DetectSku(oldRow.Name + " " + oldRow.Description) ||
                        !IsKnownPreviewKey(directSku, oldRow.PartialKey) &&
                            !(IsPreviewLabel(Str(old, "Name") + " " + Str(old, "Description")) &&
                                fresh.ProductReleaseIds.Contains(directSku + "-Preview", StringComparer.OrdinalIgnoreCase)))
                        return new(false, "Recheck Preview key",
                            "Preview identity could not be independently verified. No keys removed.",
                            false, false);
                    Invoke(old, "UninstallProductKey", null, null);
                    oldRemoved = true;
                }
                // Confirm the Preview key is actually absent before replacing.
                var afterRemoval = Read().Products.FirstOrDefault(x => x.ActivationId == oldRow.ActivationId);
                if (afterRemoval is not null && afterRemoval.HasKey &&
                    afterRemoval.PartialKey.Equals(oldRow.PartialKey, StringComparison.OrdinalIgnoreCase))
                    return new(false, "Remove Preview key",
                        "Preview key removal could not be verified. Check Office status before retrying.",
                        oldRemoved, false);
            }

            using var serviceSearch = new ManagementObjectSearcher(WmiPath,
                "SELECT * FROM SoftwareLicensingService");
            using var services = serviceSearch.Get();
            ManagementObject? service = null;
            foreach (ManagementObject item in services) { service = item; break; }
            if (service is null)
                return new(false, "Provider", "Office licensing service not found.", false, false);
            using (service)
                Invoke(service, "InstallProductKey", "ProductKey", key);

            var now = Read();
            var sku = DetectSku(oldRow.Name + " " + oldRow.Description);
            var expectedSuffix = key[^5..];
            var target = now.Products.FirstOrDefault(x =>
                (!removeOldPreview || x.ActivationId != oldRow.ActivationId) &&
                x.PartialKey.Equals(expectedSuffix, StringComparison.OrdinalIgnoreCase) &&
                DetectSku(x.Name + " " + x.Description) == sku &&
                !x.IsPreview);
            if (target is null)
                return new(false, "Verify new key",
                    oldRemoved
                        ? "New 2024 key was submitted but not verified. Preview key was removed. Check Office licensing status before retrying."
                        : "New key was submitted but no matching 2024 Volume license was verified. Check product configuration.",
                    oldRemoved, false);

            // All KMS settings are applied to the Office SKU ONLY, never to
            // SoftwareLicensingService global KMS settings used by Windows.
            using (var selected = LoadOfficeProduct(target.ActivationId))
            {
                if (!mak && kms is not null)
                {
                    Invoke(selected, "SetKeyManagementServiceMachine", "MachineName", kms.Host);
                    Invoke(selected, "SetKeyManagementServicePort", "PortNumber", (uint)kms.Port);
                }
                // Activation is part of the separately confirmed action.
                Invoke(selected, "Activate", null, null);
            }

            var verified = Read().Products.Any(x =>
                x.ActivationId == target.ActivationId && x.Status == "Licensed" &&
                x.PartialKey.Equals(expectedSuffix, StringComparison.OrdinalIgnoreCase));
            return new(verified, verified ? "Complete" : "Activation pending",
                verified ? "Selected Office product activated. Close and reopen Office applications." :
                "Key submitted but licensing is not yet verified. Inspect the Office licensing status.",
                oldRemoved, verified);
        }
        catch (Exception ex) when (ex is ManagementException or
            System.Runtime.InteropServices.COMException or UnauthorizedAccessException or
            InvalidOperationException)
        {
            // No secret key or raw WMI output is returned to the UI.
            return new(false, "Licensing operation",
                "Office activation was not completed: " + ex.GetType().Name +
                (oldRemoved ? ". Preview key was removed; repair or install the correct 2024 key." :
                    ". Original key was not removed.") +
                " Refresh inventory and check the product state.", oldRemoved, false);
        }
        finally
        {
            // The UI also clears its TextBox immediately after invocation.
            key = null;
        }
    }

    internal static bool SelfTest()
    {
        var preview = new OfficeVolumeProduct(Guid.Empty.ToString("D"),
            "Office 24, Office24VisioPro2024VL_KMS_Client Preview edition",
            "", "VisioPro2024Volume", "ABCDE", "Unlicensed", true, false,
            null, null, null, null);
        var valid = preview with
        {
            Name = "Office 24, Office24VisioPro2024VL_KMS_Client edition",
            IsPreview = false, IsSupported2024Volume = true
        };
        var inv = new OfficeVolumeInventory([preview], ["VisioPro2024Volume-Preview"], null);
        var safe = new OfficeVolumeInventory([valid], ["VisioPro2024Volume"], null);
        var realPreview = preview with { PartialKey = "WXGKQ",
            Name = "Office 24, Office24VisioPro2024VL_KMS_Client edition" };
        var realPreviewInventory = new OfficeVolumeInventory([realPreview], ["VisioPro2024Volume"], null);
        var unrelated = preview with { PartialKey = "QWERT" };
        var invalid = preview with { Name = "Office 2026 Preview", ProductId = "" };
        return DetectSku(preview.Name) == "VisioPro2024Volume" &&
            IsPreviewLabel(preview.Name) && IsUnverified2026(invalid.Name) &&
            CanReplacePreview(preview, inv) && !CanReplacePreview(invalid, inv) &&
            IsKnownPreviewKey("ProPlus2024Volume", "TX3T2") &&
            IsKnownPreviewKey("ProjectPro2024Volume", "JB89R") &&
            IsKnownPreviewKey("VisioPro2024Volume", "WXGKQ") &&
            !IsKnownPreviewKey("VisioPro2024Volume", "GCVGB") &&
            CanReplacePreview(realPreview with { IsPreview = true }, realPreviewInventory) &&
            !CanReplacePreview(unrelated, realPreviewInventory) &&
            CanApply(valid, safe) && !CanApply(invalid, inv) &&
            GvlkFor(valid) == "B7TN8-FJ8V3-7QYCP-HQPMV-YY89G" &&
            ValidateHost("kms.example.com") == "kms.example.com" &&
            ValidateHost("192.0.2.8") == "192.0.2.8" &&
            ValidateHost("https://bad.example.org") is null &&
            ValidateHost("invalid/host") is null;
    }
}
