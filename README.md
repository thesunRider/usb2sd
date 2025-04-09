# USB2SD - USB to SD Card Library for Arduino

## Overview
USB2SD is a USB-to-SD card library for Arduino (UNO, Nano, Mega) that enables mounting an SD card connected to an Arduino as a removable media device on a PC via Serial communication. Essentially, this method emulates a Mass Storage Device through Arduinos that lack native USB capabilities, allowing users to access and transfer files seamlessly.

## Features
- Supports Arduino boards without native USB support (UNO, Nano, Mega, etc.).
- Mounts an SD card connected to Arduino as a removable drive on a PC.
- Operates at a maximum serial baud rate of 2M.
- Transfer speeds of approximately 1MB per 60 seconds.
- Includes error correction and lazy writing modes.
  - Lazy writing mode improves transfer speed but skips error checking.
  - If errors are detected, the application switches to a slower, reliable mode.
- Windows application provided for easy integration.

## Applications
- Data logging
- Browsing and transferring files from an SD card via Arduino
- Remote storage applications using Arduino

## Installation
### Arduino Setup
1. Install SDFat 2.3.0 or Higher
1. Set CS PIN of Sdcard correctly on the sketch
2. Wire Default MOSI,MISO and SCK pins to the Sdcard
3. Upload the provided `USB2SD` sketch to your Arduino using the Arduino IDE.

### Windows Application Setup
1. Download the `USB2SdAdapter.exe` file from the **Release** page.
2. Run the application on your Windows machine.
3. When a device is connected, a new drive will appear automatically.
4. When the device is removed, the drive will be unmounted.

## Tech Stack
- **Arduino**: C++ (Sketch programming)
- **Windows Application**: C# (.NET 8.0, built in VS Code 2022)

## Performance
- At 2M baud rate, transferring a 1MB file takes approximately **60 seconds**.
- **Lazy Writing Mode** can **double** transfer speed by skipping error checks.
- If a file transfer error is detected, the system will revert to **slow mode** for reliability.

## Notes
- Ensure your Arduino board's serial communication is not used by other applications during operation.
- The application currently supports Windows OS only.

## License
This project is open-source. Feel free to contribute and improve performance!

## Credits
Developed by [Your Name]. Contributions and feedback are welcome!

---
For further assistance or issues, please open an issue in the repository.

