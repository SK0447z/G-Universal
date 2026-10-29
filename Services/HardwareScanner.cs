using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;

namespace GUniversal
{
    public static class HardwareScanner
    {
        private static readonly string[] SensitiveProperties = new[]
        {
            "SerialNumber",
            "UUID",
            "UniqueID",
            "MacAddress",
            "PermanentAddress"
        };

        public static bool IsSensitiveProperty(string propName)
        {
            if (string.IsNullOrEmpty(propName)) return false;
            foreach (var sensitive in SensitiveProperties)
            {
                if (propName.Contains(sensitive, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static string GetLaptopBrandAndModel()
        {
            try
            {
                string manufacturer = "";
                string model = "";
                string family = "";

                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model, SystemFamily FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        manufacturer = obj["Manufacturer"]?.ToString()?.Trim() ?? "";
                        model = obj["Model"]?.ToString()?.Trim() ?? "";
                        family = obj["SystemFamily"]?.ToString()?.Trim() ?? "";
                        break;
                    }
                }

                if (!string.IsNullOrWhiteSpace(family) &&
                    !family.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase) &&
                    !family.Equals("Default string", StringComparison.OrdinalIgnoreCase) &&
                    !model.Contains(family, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{manufacturer} {family} ({model})".Trim();
                }

                if (!string.IsNullOrWhiteSpace(manufacturer) || !string.IsNullOrWhiteSpace(model))
                {
                    if (model.StartsWith(manufacturer, StringComparison.OrdinalIgnoreCase))
                    {
                        return model;
                    }
                    return $"{manufacturer} {model}".Trim();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            return "ASUS TUF Gaming F15 (FX506LHB)";
        }

        public static string ExportDiagnosticScan(string? customPath = null)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("G-Universal Hardware & WMI Diagnostic Scan");
            sb.AppendLine($"Timestamp : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"OS        : {Environment.OSVersion}");
            sb.AppendLine($"64-Bit    : {Environment.Is64BitOperatingSystem}");
            sb.AppendLine();

            sb.AppendLine("System Information");
            AppendWmiQuery(sb, @"root\CIMV2", "SELECT Manufacturer, Model, SystemFamily, SystemSKUNumber FROM Win32_ComputerSystem");
            AppendWmiQuery(sb, @"root\CIMV2", "SELECT Manufacturer, Product, Version FROM Win32_BaseBoard");
            AppendWmiQuery(sb, @"root\CIMV2", "SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS");
            AppendWmiQuery(sb, @"root\CIMV2", "SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
            AppendWmiQuery(sb, @"root\CIMV2", "SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController");
            sb.AppendLine();

            sb.AppendLine("PnP Controller Devices");
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\CIMV2",
                    "SELECT Name, DeviceID, PNPClass, Service, Status FROM Win32_PnPEntity " +
                    "WHERE DeviceID LIKE 'HID%' OR DeviceID LIKE 'ACPI%' OR DeviceID LIKE 'USB\\VID_%'");

                foreach (ManagementObject dev in searcher.Get())
                {
                    string deviceId = dev["DeviceID"]?.ToString() ?? "";
                    string name = dev["Name"]?.ToString() ?? "Unknown";
                    string pnpClass = dev["PNPClass"]?.ToString() ?? "Unknown";
                    string service = dev["Service"]?.ToString() ?? "None";
                    string status = dev["Status"]?.ToString() ?? "";

                    sb.AppendLine($"[{pnpClass}] {name}");
                    sb.AppendLine($"    DeviceID : {deviceId}");
                    sb.AppendLine($"    Service  : {service} | Status: {status}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                sb.AppendLine($"Error scanning PnP devices: {ex.Message}");
            }
            sb.AppendLine();

            sb.AppendLine("root\\WMI ACPI Classes & Methods");
            string[] interestingKeywords = new[]
            {
                "ASUS", "ATK", "AURA", "ROG", "TUF",
                "LENOVO", "GAMEZONE", "LEGION", "IDEAPAD",
                "HP", "HPWMI", "OMEN", "VICTUS",
                "ACER", "NITRO", "PREDATOR",
                "DELL", "ALIENWARE", "AWCC",
                "THERMAL", "FAN", "BATTERY", "PERF"
            };

            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\WMI");
                scope.Connect();

                using var classSearcher = new ManagementObjectSearcher(
                    scope,
                    new ObjectQuery("SELECT * FROM meta_class"),
                    new System.Management.EnumerationOptions { ReturnImmediately = false });

                List<ManagementClass> allClasses = new List<ManagementClass>();
                foreach (ManagementClass wmiClass in classSearcher.Get())
                {
                    allClasses.Add(wmiClass);
                }

                var sortedClasses = allClasses
                    .OrderBy(c => c["__CLASS"]?.ToString() ?? "")
                    .ToList();

                foreach (ManagementClass wmiClass in sortedClasses)
                {
                    string className = wmiClass["__CLASS"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(className)) continue;

                    if (className.StartsWith("Win32_Perf", StringComparison.OrdinalIgnoreCase) ||
                        className.StartsWith("CIM_", StringComparison.OrdinalIgnoreCase) ||
                        className.StartsWith("__", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    bool hasMethods = wmiClass.Methods.Count > 0;
                    bool matchesKeyword = interestingKeywords.Any(k => className.Contains(k, StringComparison.OrdinalIgnoreCase));

                    if (!hasMethods && !matchesKeyword) continue;

                    sb.AppendLine($"Class: {className}");

                    if (wmiClass.Methods.Count > 0)
                    {
                        sb.AppendLine("  Methods:");
                        foreach (MethodData method in wmiClass.Methods)
                        {
                            List<string> inParams = new List<string>();
                            if (method.InParameters != null)
                            {
                                foreach (PropertyData p in method.InParameters.Properties)
                                {
                                    inParams.Add($"{p.Type} {p.Name}");
                                }
                            }

                            List<string> outParams = new List<string>();
                            if (method.OutParameters != null)
                            {
                                foreach (PropertyData p in method.OutParameters.Properties)
                                {
                                    outParams.Add($"{p.Type} {p.Name}");
                                }
                            }

                            sb.AppendLine($"    {method.Name}({string.Join(", ", inParams)}) -> [{string.Join(", ", outParams)}]");
                        }
                    }

                    if (wmiClass.Properties.Count > 0)
                    {
                        List<string> props = new List<string>();
                        foreach (PropertyData prop in wmiClass.Properties)
                        {
                            if (IsSensitiveProperty(prop.Name)) continue;
                            props.Add($"{prop.Name} ({prop.Type})");
                        }
                        if (props.Count > 0)
                        {
                            sb.AppendLine($"  Properties: {string.Join(", ", props)}");
                        }
                    }

                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                sb.AppendLine($"Error enumerating root\\WMI: {ex.Message}");
            }

            string targetPath = !string.IsNullOrWhiteSpace(customPath)
                ? customPath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "guniversal_hardware_scan.txt");

            try
            {
                File.WriteAllText(targetPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            return targetPath;
        }

        private static void AppendWmiQuery(StringBuilder sb, string scopePath, string query)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(scopePath, query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    List<string> items = new List<string>();
                    foreach (PropertyData prop in obj.Properties)
                    {
                        if (IsSensitiveProperty(prop.Name))
                        {
                            items.Add($"{prop.Name}: [REDACTED]");
                            continue;
                        }

                        if (prop.Value != null)
                        {
                            items.Add($"{prop.Name}: {prop.Value}");
                        }
                    }
                    sb.AppendLine("  " + string.Join(" | ", items));
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }
}
