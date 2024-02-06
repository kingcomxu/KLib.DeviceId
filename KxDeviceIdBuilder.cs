using Microsoft.Win32;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using WmiLight;

namespace KLib.DeviceId;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]

/*
 * 
 * 
AOT, 引用本类库的项目需添加对WmiLight包的引用
<ItemGroup> 
    <PackageReference Include="WmiLight" Version="5.1.1" />  
</ItemGroup>
并且在项目文件中添加
<PropertyGroup>
  <PublishWmiLightStaticallyLinked>true</PublishWmiLightStaticallyLinked>
</PropertyGroup>
 
 */

public class KxDeviceIdBuilder
{
    private SortedDictionary<string, string?> DeviceIds { get; init; } = new SortedDictionary<string, string?>();
     
    public KxDeviceIdBuilder AddComponent(string componentName, string? componentValue)
    {
        DeviceIds[componentName] = componentValue;
        return this;
    }
     
    public override string ToString()
    {
        StringBuilder stringBuilder = new StringBuilder();
        foreach (var kv in DeviceIds)
        {
            stringBuilder.Append($"{kv.Key}:{kv.Value}{Environment.NewLine}");
        }
        return stringBuilder.ToString();
    }

    public string ToHashString()
    {
        var value = string.Join(",", DeviceIds.Select(x => x.Value).ToArray());
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.Create().ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
     

    public KxDeviceIdBuilder AddMachineName()
    {
        return AddComponent("MachineName", Environment.MachineName);
    }

    public KxDeviceIdBuilder AddOSVersion()
    {
        return AddComponent("OSVersion", Environment.OSVersion.ToString());
    }
     
    public KxDeviceIdBuilder AddMacAddress()
    {
        static string FormatMacAddress(string input)
        {
            // Check if this can be a hex formatted EUI-48 or EUI-64 identifier.
            if (input.Length != 12 && input.Length != 16)
            {
                return input;
            }

            // Chop up input in 2 character chunks.
            const int partSize = 2;
            var parts = Enumerable.Range(0, input.Length / partSize).Select(x => input.Substring(x * partSize, partSize));

            // Put the parts in the AA:BB:CC format.
            var result = string.Join(":", parts.ToArray());

            return result;
        }
        //Wireless  NetworkInterfaceType.Wireless80211
        //DockerBridge x.Name == docker0

        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            .Where(x => !x.Name.Contains("docker0", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.Name.Contains("VMware", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.Name.Contains("TAP", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.Name.Contains("蓝牙"))
            .Where(x => !x.Description.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.Description.Contains("Virtual Adapter", StringComparison.OrdinalIgnoreCase))
            ;

         

        var values = networkInterfaces
            .Select(x => x.GetPhysicalAddress().ToString())
            .Where(x => x != "000000000000")
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => FormatMacAddress(x))
            .ToList();

        values.Sort();

        var componentValue = values.Count > 0
            ? string.Join(",", values.ToArray())
            : null;

        return this.AddComponent("MacAddress", componentValue); 
    }


    #region WMI

    public KxDeviceIdBuilder AddProcessorInfo()
    {

        string? componentValue = null;

        try
        {
            using WmiConnection wmiConnection = new WmiConnection();
            foreach (WmiObject wmiObject in wmiConnection.CreateQuery($"SELECT * FROM Win32_Processor"))
            {
                
                var processorId = wmiObject["ProcessorId"] as string;
                var caption = wmiObject["Caption"] as string;
                var name = wmiObject["Name"] as string;

                var numberOfCores = (uint)wmiObject["NumberOfCores"];
                var numberOfLogicalProcessors=(uint)wmiObject["NumberOfLogicalProcessors"];
                 



                if (!string.IsNullOrEmpty(processorId) || !string.IsNullOrEmpty(caption) || !string.IsNullOrEmpty(name))
                {
                    componentValue = $"{numberOfCores}-Core{numberOfLogicalProcessors}-Thread|Name:{name}|Caption:{caption}|ProcessorId:{processorId}";
                    break;
                }
            }
        }
        catch
        {

        }
         
        return this.AddComponent("ProcessorInfo", componentValue);
    }

    public KxDeviceIdBuilder AddSystemUuid()
    {
        var componentValue = GetWMIValue("UUID", "Win32_ComputerSystemProduct");
        return this.AddComponent("SystemUuid", componentValue);
    }

    public KxDeviceIdBuilder AddMotherboardInfo()
    {
        string? componentValue = null;

        try
        {
            using WmiConnection wmiConnection = new WmiConnection();
            foreach (WmiObject wmiObject in wmiConnection.CreateQuery($"SELECT * FROM Win32_BaseBoard"))
            {
                var serialNumber = wmiObject["SerialNumber"] as string;
                var product = wmiObject["Product"] as string;
                var manufacturer = wmiObject["Manufacturer"] as string;

                if (!string.IsNullOrEmpty(serialNumber) || !string.IsNullOrEmpty(product) || !string.IsNullOrEmpty(manufacturer))
                {
                    componentValue =  $"SerialNumber:{serialNumber}|Product:{product}|Manufacturer:{manufacturer}";
                    break;
                }
            }
        }
        catch
        {

        }
         
         
        return this.AddComponent("MotherboardInfo", componentValue);
    }
     
    public KxDeviceIdBuilder AddSystemDriveInfo()
    {
        //https://github.com/MatthewKing/DeviceId/blob/main/src/DeviceId.Windows.Wmi/Components/WmiSystemDriveSerialNumberDeviceIdComponent.cs
        //https://stackoverflow.com/questions/57558122/is-it-possible-to-determine-the-win32-diskdrive-serialnumber-of-the-environment?rq=3
        string? componentValue = null;

        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);

        // SystemDirectory can sometimes be null or empty.
        // See: https://github.com/dotnet/runtime/issues/21430 and https://github.com/MatthewKing/DeviceId/issues/64
        if (string.IsNullOrEmpty(systemDirectory) || systemDirectory.Length < 2)
        {
            return AddComponent("SystemDriveInfo", componentValue);
        }

        var systemLogicalDiskDeviceId = systemDirectory.Substring(0, 2);
         
        try
        { 
            var queryString = $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{systemLogicalDiskDeviceId}'}} WHERE ResultClass = Win32_DiskPartition";
            using WmiConnection wmiConnection = new WmiConnection();
            foreach (WmiObject wmiObject in wmiConnection.CreateQuery(queryString))
            {
                if (wmiObject["DeviceId"] is string deviceId)
                {
                    var queryString2 = $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{deviceId}'}} WHERE ResultClass=Win32_DiskDrive";

                    foreach (var wmiObject2 in wmiConnection.CreateQuery(queryString2))
                    {
                        string? serialNumber = wmiObject2["SerialNumber"] as string;
                        string? caption = wmiObject2["Caption"] as string;
                        string? firmwareRevision = wmiObject2["FirmwareRevision"] as string;

                        if (!string.IsNullOrEmpty(serialNumber) || !string.IsNullOrEmpty(caption) || !string.IsNullOrEmpty(firmwareRevision))
                        {
                            componentValue = $"SerialNumber:{serialNumber}|Caption:{caption}|FirmwareRevision:{firmwareRevision}";
                        }
                    }
                }
            }
        }
        catch { }

        return AddComponent("SystemDriveInfo", componentValue); 
    }
     
    private string? GetWMIValue(string propertyName, string className)
    {
        var values = new List<string>();

        try
        {
            using WmiConnection wmiConnection = new WmiConnection();
            foreach (WmiObject wmiObject in wmiConnection.CreateQuery($"SELECT {propertyName} FROM {className}"))
            {
                if (wmiObject[propertyName] is string value)
                {
                    values.Add(value);
                }

            }
        }
        catch
        {

        }

        values.Sort();

        return values.Count > 0
            ? string.Join(",", values.ToArray())
            : null;
    }

    #endregion

    #region Win32Registry

    public KxDeviceIdBuilder AddWindowsRegistryToken(string? valueName = null)
    {
        if (valueName == null)
        {
            valueName = Process.GetCurrentProcess().ProcessName;
        }

        string keyName = @"SOFTWARE\KLib.DeviceIdToken";

        string? componentValue = null;

        try
        {
            using var registry = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            using var subKey = registry.CreateSubKey(keyName, true);
            if (subKey != null)
            {
                var valueStr = subKey.GetValue(valueName) as string;
                if (!string.IsNullOrEmpty(valueStr))
                {
                    componentValue = valueStr;
                }
                else
                {
                    var valueStrNew = Guid.NewGuid().ToString();
                    subKey.SetValue(valueName, valueStrNew);
                    componentValue = valueStrNew;
                }
            }
        }
        catch { }

        this.AddComponent("WindowsRegistryToken", componentValue);

        return this;
    }

     
    public KxDeviceIdBuilder AddWindowsDeviceId()
    {
        return AddComponent("WindowsDeviceId", GetRegistryValue(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\SQMClient", "MachineId"));
    }
     
    public KxDeviceIdBuilder AddWindowsProductId()
    {
        return AddComponent("WindowsProductId", GetRegistryValue(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductId")); 
    }
     
    public KxDeviceIdBuilder AddMachineGuid()
    {
        return AddComponent("MachineGuid", GetRegistryValue(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Cryptography", "MachineGuid")); 
    }

     
    private static RegistryView[] Both32BitAnd64BitRegistryViews { get; } = new[] { RegistryView.Registry32, RegistryView.Registry64 }; 
    private static string? GetRegistryValue(RegistryHive registryHive, string keyName, string valueName)
    { 
        foreach (var registryView in Both32BitAnd64BitRegistryViews)
        {
            try
            {
                using var registry = RegistryKey.OpenBaseKey(registryHive, registryView);
                using var subKey = registry.OpenSubKey(keyName);
                if (subKey != null)
                {
                    var value = subKey.GetValue(valueName);
                    var valueAsString = value?.ToString();
                    if (!string.IsNullOrEmpty(valueAsString))
                    {
                        return valueAsString;
                    }
                }
            }
            catch { }
        }

        return null; 
    }


    #endregion
}


