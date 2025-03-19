using System;
using System.IO.Ports;
using System.Threading;
using System.Management;
using System.Runtime.InteropServices;
using System.IO;
using IMAPI2;
using IMAPI2FS;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Collections;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Drawing;

class USBSDAdapter
{
    public const byte ACK_BIT = 0x20;
    public const byte COMMAND_START_BIT = 0x21;
    public const byte COMMAND_END_BIT = 0x22;

    public const byte SUBCMD_SDCARD_SIZE = 0x1;
    public const byte SUBCMD_SDCARD_FREE_SPACE = 0x2;
    public const byte SUBCMD_SDCARD_FATTYPE = 0x3;
    public const byte SUBCMD_FILES_LIST = 0x4;
    public const byte SUBCMD_FILES_GET = 0x5;
    public const byte SUBCMD_CHECK_DEV = 0x6;

    public const byte DEV_PRINT = 0xBF;
    static string portName = "";  // Change this to your actual COM port
    static int baudRate_default = 2000000;
    static byte[] detection_print = new byte[] { ACK_BIT, COMMAND_START_BIT, ACK_BIT, DEV_PRINT, ACK_BIT, DEV_PRINT, COMMAND_END_BIT };
    static byte[] check_print = new byte[] { ACK_BIT, COMMAND_START_BIT, SUBCMD_CHECK_DEV, 0, ACK_BIT };

    public static bool card_connected = false;
    public static SerialPort serialPort;
    public static string path_temp_vdisk = Path.Combine(Path.GetTempPath(), "temp_storage.vhd");
    public static char mount_point;
    static private ManagementEventWatcher watcher = new ManagementEventWatcher();
    public static Vdisk vdisk_m = new Vdisk();
    public static bool disk_mounted = false;

    static void Main()
    {

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("This program is only supported on Windows.");
            Environment.Exit(0);
        }

        watcher.Query = new WqlEventQuery("SELECT * FROM __InstanceOperationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBControllerDevice'");
        watcher.EventArrived += new EventArrivedEventHandler(DeviceChangedEvent);
        watcher.Start();

        Console.CancelKeyPress += new ConsoleCancelEventHandler(CancelKeyPressHandler);

        handle_serial_find();


        while (true)
        {
            Thread.Sleep(100);
        }
    }

    private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs e)
    {
        Console.WriteLine("\nCtrl+C detected. Exiting...");
        // Set the Cancel property to true to prevent the default termination of the process
        e.Cancel = true;
        if (card_connected)
            serialPort.Close();

        watcher.Stop();
        Environment.Exit(0);
    }

    private static (bool error, string response, byte[] payload) parse_results(byte[] bufferin, int valid_bytes)
    {
        bool error = false;
        string response = "";
        byte[] payload = { };


        if (bufferin[0] != ACK_BIT || bufferin[1] != COMMAND_START_BIT || bufferin[valid_bytes - 1] != COMMAND_END_BIT) return (true, response, payload);
        response = bufferin[3].ToString();
        payload = bufferin.Skip(5).Take(valid_bytes - 6).ToArray();
        return (error, response, payload);
    }

    private static int get_sdcard_freesize()
    {
        string response;
        byte[] payload;

        byte[] sendBytes = new byte[] { ACK_BIT, COMMAND_START_BIT, SUBCMD_SDCARD_FREE_SPACE, 0, ACK_BIT };
        serialPort.DiscardInBuffer();
        serialPort.Write(sendBytes, 0, sendBytes.Length);
        byte[] buffer = new byte[10];
        WaitForSerialData(10);
        int bytesRead = serialPort.Read(buffer, 0, buffer.Length);

        var parse = parse_results(buffer, bytesRead);
        if (parse.error) throw new Exception("There was an error in response while Reading Card FreeSize");
        int card_size = BitConverter.ToInt32(parse.payload, 0);
        return card_size;
    }

    private static (byte[], int, bool) WaitForSerialBytes(int bytesToRead, int timeout, int noDataTimeout)
    {
        DateTime startTime = DateTime.Now;
        DateTime lastReadTime = DateTime.Now; // Track last read time

        byte[] buffer = new byte[bytesToRead];
        int bytesRead = 0;
        bool error = false;

        while ((DateTime.Now - startTime).TotalMilliseconds < timeout && bytesRead < bytesToRead)
        {
            // If bytes are available, read them into the buffer
            if (serialPort.BytesToRead > 0)
            {
                int bytesToReadNow = Math.Min(serialPort.BytesToRead, bytesToRead - bytesRead);
                int numBytes = serialPort.Read(buffer, bytesRead, bytesToReadNow);
                bytesRead += numBytes;
                lastReadTime = DateTime.Now; // Update last read time
                startTime = DateTime.Now;
            }

            // Check if bytesRead has not increased for noDataTimeout duration
            if ((DateTime.Now - lastReadTime).TotalMilliseconds > noDataTimeout)
            {
                Console.WriteLine("No data received within the expected time.");
                error = true;
                break;
            }

            Thread.Sleep(150); // Small delay to prevent excessive CPU usage
        }

        // If we didn't get the required number of bytes within timeout, throw an exception
        if (bytesRead < bytesToRead)
        {
            Console.WriteLine($"Timeout: Could not read the required number of bytes within the specified time. Bytes= {bytesRead} / {bytesToRead}");
            error = true;
        }

        return (buffer, bytesRead, error); // Return the buffer with received data
    }


    private static void WaitForSerialData(int expectedBytes, int timeout = 5000)
    {
        DateTime startTime = DateTime.Now;

        // Wait until enough bytes are available or timeout occurs
        while (serialPort.BytesToRead < expectedBytes)
        {
            // If timeout has expired, throw an exception
            if ((DateTime.Now - startTime).TotalMilliseconds > timeout)
            {
                throw new Exception($"Timeout occurred while waiting for serial data Bytes read={serialPort.BytesToRead}.");
            }
            //Console.WriteLine($"Buffer Size Data ={serialPort.BytesToRead}");
            Thread.Sleep(100);
        }
    }

    private static byte[] get_sdcard_filedata(string name_file, int size)
    {
        byte SPEED_MODE = 1; //faster speed (less accuracy)

    restart_aquisition:
        string response;
        byte[] payload;
        string name = name_file + "\n";

        serialPort.Close();
        serialPort.Open();
        Thread.Sleep(100);

        byte[] sendBytes = new byte[5 + name.Length];
        sendBytes[0] = ACK_BIT;
        sendBytes[1] = COMMAND_START_BIT;
        sendBytes[2] = SUBCMD_FILES_GET;
        sendBytes[3] = SPEED_MODE;
        Encoding.ASCII.GetBytes(name).CopyTo(sendBytes, 4);
        sendBytes[sendBytes.Length - 1] = ACK_BIT;

        serialPort.DiscardInBuffer();
        serialPort.Write(sendBytes, 0, sendBytes.Length);

        var responseserial = WaitForSerialBytes(9 + size, 5000, 5000); //new byte[9+ size];
        if (responseserial.Item3)
        {
            SPEED_MODE = 0; //slower speed (more accuracy)
            Console.WriteLine($"Error in response while Reading Card File {name_file},Restarting in 2 sec with SPEED_MODE={SPEED_MODE}..");
            serialPort.Close();
            serialPort.Open();
            Thread.Sleep(2000);
            goto restart_aquisition;

        }
        byte[] buffer = responseserial.Item1;
        int bytesRead = responseserial.Item2;



        //WaitForSerialData(9 + size,20000);

        //int bytesRead = serialPort.Read(buffer, 0, buffer.Length);

        var parse = parse_results(buffer, bytesRead);
        if (parse.error) throw new Exception($"There was an error in response while Reading Card File {name_file}");


        byte[] file_size2 = buffer.Skip(3).Take(4).ToArray();
        byte[] file_payload = buffer.Skip(8).Take(buffer.Length - 9).ToArray();

        //Console.WriteLine("-----------------BUFFERDOWN");
        //Console.WriteLine(BitConverter.ToString(buffer).Replace("-", " "));
        //Console.WriteLine("-----------------PAYLOADDOWN");
        //Console.WriteLine(BitConverter.ToString(file_payload).Replace("-", " "));


        if (file_payload.Length != size && size != BitConverter.ToInt32(file_size2, 0))
            throw new Exception($"File Size Mismatch for {name_file}: {parse.payload.Length} != {size} ");
        return file_payload;
    }

    private static int get_sdcard_size() {
        string response;
        byte[] payload;

        byte[] sendBytes = new byte[] { ACK_BIT, COMMAND_START_BIT, SUBCMD_SDCARD_SIZE, 0, ACK_BIT };
        serialPort.DiscardInBuffer();
        serialPort.Write(sendBytes, 0, sendBytes.Length);
        byte[] buffer = new byte[10];
        WaitForSerialData(10);
        int bytesRead = serialPort.Read(buffer, 0, buffer.Length);

        var parse = parse_results(buffer, bytesRead);
        if (parse.error) throw new Exception("There was an error in response while Reading Card Size");
        int card_size = BitConverter.ToInt32(parse.payload, 0);
        if (card_size < 100) throw new Exception("Invalid Card Size");
        return card_size;
    }

    private static bool ValidateFileStructure(string input)
    {
        // Split the string by semicolon (;) separator
        string[] parts = input.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        // Regex pattern to match the filename and size
        string pattern = @"^[^<]+?\s*<#>\s*\d+$";

        // Check each part against the regex
        foreach (string part in parts)
        {
            if (!Regex.IsMatch(part.Trim(), pattern))
            {
                // If any part doesn't match, return false
                return false;
            }
        }

        // If all parts are valid
        return true;
    }

    private static List<(int size, string name)> get_sdcard_filelist()
    {
        string response;
        byte[] payload;

    restart_filelist:
        byte[] sendBytes = new byte[] { ACK_BIT, COMMAND_START_BIT, SUBCMD_FILES_LIST, 0, ACK_BIT };
        serialPort.DiscardInBuffer();
        serialPort.Write(sendBytes, 0, sendBytes.Length);
        byte[] buffer = new byte[10000];
        Thread.Sleep(5000);
        int bytesRead = serialPort.Read(buffer, 0, buffer.Length);

        var parse = parse_results(buffer, bytesRead);
        if (parse.error) throw new Exception("There was an error in response while Reading Card File List");
        string file_list = Encoding.ASCII.GetString(parse.payload);

        Console.WriteLine(file_list);
        if (!ValidateFileStructure(file_list))
        {
            Console.WriteLine("Invalid file list structure.Retrying in 2 sec..");
            Thread.Sleep(2000);
            goto restart_filelist;
        }

        string pattern = @"([^<;]+?)\s*<#>\s*(\d+)(?=\s*;|$)";
        var matches = Regex.Matches(file_list, pattern);


        var result = new List<(int filesize, string filename)>();
        foreach (Match match in matches)
        {
            string filename = match.Groups[1].Value;
            int filesize = int.Parse(match.Groups[2].Value);
            result.Add((filesize, filename));
        }
        return result;
    }

    private static void handle_card_serial(string port)
    {
        try
        {
            serialPort = new SerialPort(port, baudRate_default, Parity.None, 8, StopBits.One);

            serialPort.ReadBufferSize = 100000;  // Set RX buffer size (default: 4096 bytes)
            serialPort.WriteBufferSize = 100000; // Set TX buffer size (default: 2048 bytes)

            serialPort.ReadTimeout = 10000;   // Set timeout for reading
            serialPort.WriteTimeout = 1000;  // Set timeout for writing

            serialPort.Open();
            Thread.Sleep(3000);
            card_connected = true;
            serialPort.DiscardInBuffer();

            int card_size = get_sdcard_size();
            int card_freesize = get_sdcard_freesize();
            Console.WriteLine($"Card size={card_freesize} / {card_size} KB");
            var filesizeList = get_sdcard_filelist();

            path_temp_vdisk = Path.Combine(Path.GetTempPath(), "temp_storage.vhd");
            mount_point = vdisk_m.CreateMountVDisk(path_temp_vdisk, "BIO_TOUCH", 200);
            disk_mounted = true;

            //Thread.Sleep(5000);
            //vdisk_m.DismountVDisk(path_temp_vdisk);

            foreach (var item in filesizeList)
            {
                if (item.size < 5) continue;
                //if (!((item.name).EndsWith(".csv", StringComparison.OrdinalIgnoreCase))) continue; //remove when ready
                Console.WriteLine($"Pulling Filesize: {item.size} Bytes, FileName: {item.name}");
                byte[] data_buffer = get_sdcard_filedata(item.name, item.size);
                File.WriteAllBytes(mount_point + ":\\" + item.name, data_buffer);
                Console.WriteLine("collecting finished..");

            }

            Console.WriteLine("Finished collecting files");

            //remove when ready
            /**
            serialPort.Close();
            watcher.Stop();
            if (disk_mounted)
                vdisk_m.DismountVDisk(path_temp_vdisk);
            Environment.Exit(0);
            **/
        }
        catch (Exception ex)
        {

            Console.WriteLine($"Error on Mounting {port} as SD: {ex.Message}");

            if (card_connected)
            {
                try
                {
                    byte[] buffer = new byte[1000];
                    int bytesRead = serialPort.Read(buffer, 0, buffer.Length);
                    Console.WriteLine("Serial dump:");
                    Console.WriteLine(BitConverter.ToString(buffer).Replace("-", " "));
                    serialPort.Close();
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"Error on Mounting {port} as SD: {ex2.Message}");
                }


                if (disk_mounted)
                {
                    vdisk_m.DismountVDisk(path_temp_vdisk);
                    disk_mounted = false;
                }

                card_connected = false;
            }

            //serialPort.Write(check_print, 0, check_print.Length);
            //serialPort.Close();

        }
    } 

    private static void handle_serial_find()
    {
        portName = FindMatchingCOMPort(check_print, detection_print);
        if (portName != null)
        {   Console.WriteLine($"Matching COM Port Found: {portName}");
            handle_card_serial(portName);  }
        else
            Console.WriteLine("No matching COM port found.");
    }

    private static void DeviceChangedEvent(object sender, EventArrivedEventArgs e)
    {
        string eventType = e.NewEvent.ClassPath.ClassName;

        Console.WriteLine("Event type: " + eventType);

        // COM port change detection
        if (eventType == "__InstanceCreationEvent")
        {
            Console.WriteLine("A new device was plugged in.");

            handle_serial_find();
        }
        else if (eventType == "__InstanceDeletionEvent")
        {
            if (card_connected && !SerialPort.GetPortNames().Contains(portName)){
                //serial connection lost disconnect and reconnect
                //unmounting drive
                if (disk_mounted)
                {
                    vdisk_m.DismountVDisk(path_temp_vdisk);
                    disk_mounted = false;
                }
                card_connected = false;
            }



            Console.WriteLine("A device was unplugged.");
            handle_serial_find();
        }
    }

    static string FindMatchingCOMPort(byte[] sendBytes, byte[] expectedResponse)
    {
        foreach (string port in SerialPort.GetPortNames())  // Iterate through all available COM ports
        {
            try
            {
                using (SerialPort srlPrt = new SerialPort(port, baudRate_default, Parity.None, 8, StopBits.One))
                {
                    Console.WriteLine($"Checking on {port}");
                    srlPrt.ReadTimeout = 100;   // Set timeout for reading
                    srlPrt.WriteTimeout = 100;  // Set timeout for writing
                    srlPrt.Open();  // Open the COM port

                    Thread.Sleep(1000);
                    srlPrt.DiscardInBuffer();  // Clear any existing data
                    srlPrt.Write(sendBytes, 0, sendBytes.Length);  // Send test byte(s)

                    byte[] buffer = new byte[expectedResponse.Length];
                    Thread.Sleep(1000);  
                    int bytesRead = srlPrt.Read(buffer, 0, buffer.Length);  // Read response
                    Console.WriteLine(BitConverter.ToString(buffer).Replace("-", " "));
                    if (bytesRead == expectedResponse.Length && CompareArrays(buffer, expectedResponse))
                    {
                        srlPrt.Close();
                        return port;  // Return the COM port name if a match is found
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error on {port}: {ex.Message}");
            }
        }
        return null;  // No matching COM port found
    }

    static bool CompareArrays(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}