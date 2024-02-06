# KLib.DeviceId
  Generate unique hardware id in C# (windows only), .Net NativeAot friendly
## Quick Start  
```csharp
KxDeviceIdBuilder builder = new KxDeviceIdBuilder();
builder
    .AddMachineName()              //from Environment
    .AddOSVersion()                //from Environment 
    .AddMacAddress()                //NetworkInterface 
    .AddMachineGuid()               //from registry LocalMachine SOFTWARE\Microsoft\Cryptography MachineGuid
    .AddWindowsProductId()          //from registry LocalMachine SOFTWARE\Microsoft\Windows NT\CurrentVersion ProductId
    .AddWindowsDeviceId()           //from registry LocalMachine SOFTWARE\Microsoft\SQMClient MachineId
    .AddWindowsRegistryToken()      //from registry CurrentUser SOFTWARE\KLib.DeviceIdToken valueName ?? Process.GetCurrentProcess().ProcessName 
    .AddProcessorInfo()             //WmiLight  SELECT * FROM Win32_Processor
    .AddSystemUuid()                //WmiLight  UUID FROM Win32_ComputerSystemProduct
    .AddMotherboardInfo()           //WmiLight  SELECT * FROM Win32_BaseBoard 
    .AddSystemDriveInfo()           //WmiLight  Win32_DiskPartition Win32_DiskDrive 
    ;

 
Console.WriteLine(builder.ToString()); 
Console.WriteLine(builder.ToHashString()); 
```
##Links
- https://github.com/MatthewKing/DeviceId
- https://github.com/MartinKuschnik/WmiLight
