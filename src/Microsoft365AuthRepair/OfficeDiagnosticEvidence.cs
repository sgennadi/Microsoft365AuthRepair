namespace Microsoft365AuthRepair;

// Immutable read-only and privacy-safe Office signals for a PUBLIC diagnostic
// report. No product GUID, PID, partial/full key, KMS hostname, build, UPN,
// tenant, user token, file path, raw WMI rows or license text can leave here.
internal sealed record OfficeDiagnosticEvidence(
    string Installed, string Edition, string Activation,
    int WmiRecords, int LicensedRecords, int PreviewRecords, int PreviewKeyRecords)
{
    private static string EditionFromRelease(IReadOnlyList<string> releaseIds)
    {
        var supported = releaseIds.Where(x => x is
            "ProPlus2024Volume" or "Standard2024Volume" or
            "VisioPro2024Volume" or "VisioStd2024Volume" or
            "ProjectPro2024Volume" or "ProjectStd2024Volume").Distinct().ToArray();
        if (supported.Length > 1) return "MULTIPLE_VOLUME";
        return supported.Length == 1 ? supported[0] switch
        {
            "ProPlus2024Volume" => "PROPLUS_2024",
            "Standard2024Volume" => "STANDARD_2024",
            "VisioPro2024Volume" or "VisioStd2024Volume" => "VISIO_2024",
            "ProjectPro2024Volume" or "ProjectStd2024Volume" => "PROJECT_2024",
            _ => "VOLUME_OTHER"
        } : "VOLUME_OTHER";
    }

    internal static OfficeDiagnosticEvidence FromInventory(
        OfficeLicenseSnapshot office, OfficeVolumeInventory? volume)
    {
        if (!office.Installed)
            return new("UNKNOWN", "UNKNOWN", "UNKNOWN", 0, 0, 0, 0);
        if (office.ProductFamily == "Microsoft 365 Apps subscription")
            return new("YES", "MICROSOFT_365", "NOT_VERIFIED", 0, 0, 0, 0);
        if (office.ProductFamily == "Office / Project / Visio Retail")
            return new("YES", "RETAIL", "NOT_VERIFIED", 0, 0, 0, 0);
        if (office.ProductFamily != "Office volume / LTSC")
            return new("YES", "UNKNOWN", "NOT_VERIFIED", 0, 0, 0, 0);

        if (volume is null || !volume.InstalledProductsVerified ||
            !string.IsNullOrEmpty(volume.Warning))
            return new("YES", "VOLUME_OTHER", "UNKNOWN", 0, 0, 0, 0);

        var installed = volume.Products.Where(x =>
            OfficeVolumeLicensing.MatchesInstalledProduct(x, volume)).ToArray();
        int active = installed.Count(x =>
            OfficeVolumeLicensing.IsActive(x) && !x.IsPreview);
        int preview = installed.Count(x => x.IsPreview);
        int previewWithKey = installed.Count(x => x.IsPreview && x.HasKey);
        string activation = active > 0 ? "LICENSED" :
            previewWithKey > 0 ? "PREVIEW_KEY_PRESENT" :
            installed.Any(x => x.HasKey && !x.IsPreview) ? "UNLICENSED" :
            "UNKNOWN";
        return new("YES", EditionFromRelease(volume.ProductReleaseIds), activation,
            installed.Length, active, preview, previewWithKey);
    }

    internal static OfficeDiagnosticEvidence Capture()
    {
        // Do not access the affected user's HKCU through an elevated admin.
        // Subscription status is explicitly NOT_VERIFIED without user consent.
        var machine = OfficeLicenseDiagnostics.Capture(ownDesktopSession: false);
        if (!machine.Installed || machine.ProductFamily != "Office volume / LTSC")
            return FromInventory(machine, null);
        return FromInventory(machine, OfficeVolumeLicensing.Read());
    }

    internal static string CountBand(int count) => count switch
    {
        <= 0 => "NONE",
        1 => "ONE",
        2 => "TWO",
        _ => "THREE_PLUS"
    };

    internal static bool SelfTest()
    {
        var machine = new OfficeLicenseSnapshot(true, "Office volume / LTSC",
            "Unknown", null, null, false, null, null, null, false);
        var installedSku = "ProPlus2024Volume";
        var active = new OfficeVolumeProduct(Guid.NewGuid().ToString("D"),
            "Office24ProPlus2024VL_KMS_Client edition", "", installedSku,
            "GCVGB", "Licensed", false, true, "internal.example", 1688, null, null);
        var preview = active with { ActivationId = Guid.NewGuid().ToString("D"),
            Name = "Office24ProPlus2024PreviewVL_KMS_Client", PartialKey = "",
            Status = "Unlicensed", IsPreview = true, IsSupported2024Volume = false };
        var other = active with { ActivationId = Guid.NewGuid().ToString("D"),
            PartialKey = "", Status = "Unlicensed" };
        var volume = new OfficeVolumeInventory([active, active, preview, other],
            [installedSku], null) { InstalledProductsVerified = true };
        var evidence = FromInventory(machine, volume);
        var subscription = machine with { ProductFamily = "Microsoft 365 Apps subscription" };
        var unavailable = FromInventory(machine, volume with
        {
            Warning = "SECRET_PATH INTERNAL_HOST",
            InstalledProductsVerified = false
        });
        var noInstalled = FromInventory(machine with { Installed = false }, volume);
        return evidence.Installed == "YES" &&
            evidence.Edition == "PROPLUS_2024" &&
            evidence.Activation == "LICENSED" &&
            evidence.WmiRecords == 4 &&
            evidence.LicensedRecords == 2 &&
            evidence.PreviewRecords == 1 &&
            evidence.PreviewKeyRecords == 0 &&
            FromInventory(subscription, volume).Activation == "NOT_VERIFIED" &&
            unavailable.Activation == "UNKNOWN" &&
            noInstalled.Installed == "UNKNOWN" &&
            CountBand(0) == "NONE" && CountBand(2) == "TWO" &&
            CountBand(50) == "THREE_PLUS";
    }
}
