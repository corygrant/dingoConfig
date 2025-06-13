# dingoConfig Specification

## Project Overview

- Build a desktop application using ASP.NET Core backend with React frontend.
- Application is for reading/displaying CAN or USB CDC data from multiple devices at once.
- The application runs locally with no internet connectivity required.

## 

## Technology 

- ASP.NET Core
- SignalR
- React
- MUI
- Typescript

>GATE: Backend must be working and tested before frontend development**

## Communication

Possible communication hardware:
- SLCAN - Serial CAN using USB CDC devices
- PCAN - Peak PCAN USB adapter
- USB - USB CDC directly to device, no CAN

## Device Control

Each device in the configuration should have the ability to:
- Connect
- Disconnect
- Read (upload settings from device)
- Download (write settings to device)
- Burn (device stores values in non-volatile memory)
- Enter firmware update (device enter bootloader)
- Write (send messages cyclically, example: simulated CAN keypad)

## Device Catalog

- The application should handle devices generically with catalog JSON files used to describe the device properties/data. 
- Each property and datapoint should have DBC-like definitions that are used to build/parse messages to/from the devices
- This will give maximum flexibility to add or modify devices in the future
- The catalog files should load on application open

## Example Settings Read/Write from Previous Application

This should be handled by a DBC-like function using the catalog file to define the message format

Read:
```csharp
if (data.Length != 7) return false;

Enabled = Convert.ToBoolean(data[2] & 0x01);
Mode = (InputMode)((data[2] & 0x06) >> 1);
TimeoutEnabled = Convert.ToBoolean((data[2] & 0x08) >> 3);
Operator = (Operator)((data[2] & 0xF0) >> 4);
DLC = (data[3] & 0xF0) >> 4;
StartingByte = (data[3] & 0x0F);
OnVal = (data[4] << 8) + data[5];
Timeout = data[6] / 10.0;
return true;
```

Write:
```csharp
byte[] data = new byte[8];
data[0] = Convert.ToByte(MessagePrefix.CanInputs);
data[1] = Convert.ToByte(Number - 1);
data[2] = Convert.ToByte(((Convert.ToByte(Operator) & 0x0F) << 4) +
          ((Convert.ToByte(Mode) & 0x03) << 1) +
          Convert.ToByte((Convert.ToByte(TimeoutEnabled) << 3)) +
          (Convert.ToByte(Enabled) & 0x01));
data[3] = Convert.ToByte(((DLC & 0x0F) << 4) +
          (Convert.ToByte(StartingByte) & 0x0F));
data[4] = Convert.ToByte((OnVal & 0xFF00) >> 8); 
data[5] = Convert.ToByte(OnVal & 0x00FF);
data[6] = Convert.ToByte((Timeout * 10));
return data;
```

## Configuration File

- Device configuration can be saved/opened using a configuration JSON file.
- See dingoConfig_ExampleFile.json for an example of the full configuration. 

## Simulation

- Each device should have the ability to be simulated for testing purposes 

## Final Application

- Connect to multiple devices (mocked initially)
- Load device catalogs from JSON files
- Configure devices through web interface
- Display real-time data and plot
- Save/load configuration JSON files
- Single executable deployment
- No internet connectivity required