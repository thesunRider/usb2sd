using System;
using System.Diagnostics;
using System.IO;

class Vdisk
{
    private char GetAvailableDriveLetter()
    {
        // List of all possible drive letters (A-Z)
        char[] allDriveLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

        // Get all the drives currently mounted
        DriveInfo[] drives = DriveInfo.GetDrives();

        // List all the drive letters that are already in use
        var usedLetters = new HashSet<char>();
        foreach (var drive in drives)
        {
            if (drive.IsReady) // Ensure the drive is ready
            {
                usedLetters.Add(drive.Name[0]); // Extract drive letter
            }
        }

        // Find the first unused drive letter
        foreach (char driveLetter in allDriveLetters)
        {
            if (!usedLetters.Contains(driveLetter))
            {
                return driveLetter;
            }
        }

        // Return null if no drive letter is available
        return 'K';
    }

    public void DismountVDisk(string vhdPath)
    {
        string script = GenerateDiskPartScriptDismount(vhdPath);

        // Write the script to a temporary file
        string scriptFilePath = Path.Combine(Path.GetTempPath(), "create_vhd_script.txt");
        File.WriteAllText(scriptFilePath, script);

        // Run DiskPart with the script
        RunDiskPartScript(scriptFilePath);
        if (File.Exists(vhdPath))
        {
            File.Delete(vhdPath);
            Console.WriteLine("VHD deleted successfully.");
        }
    }


    public char CreateMountVDisk(string vhdPath, string vhdLabel,int vhdSizeMB)
    {
        // Create the DiskPart script
        char mountpoint = GetAvailableDriveLetter();
        string script = GenerateDiskPartScriptCreateMount(vhdPath, vhdSizeMB, vhdLabel, mountpoint);

        // Write the script to a temporary file
        string scriptFilePath = Path.Combine(Path.GetTempPath(), "create_vhd_script.txt");
        File.WriteAllText(scriptFilePath, script);

        // Run DiskPart with the script
        RunDiskPartScript(scriptFilePath);
        return mountpoint;
    }

    static string GenerateDiskPartScriptDismount(string vhdPath)
    {
        return $@"
select vdisk file=""{vhdPath}""
detach vdisk
";
    }

    static string GenerateDiskPartScriptCreateMount(string vhdPath, int sizeMB, string label,char mountpoint)
    {
        return $@"
create vdisk file=""{vhdPath}"" type=EXPANDABLE maximum={sizeMB}
select vdisk file=""{vhdPath}""
attach vdisk
create partition primary
select partition 1
format fs=fat32 quick label=""{label}""
assign letter={mountpoint}
";
    }

    static void RunDiskPartScript(string scriptFilePath)
    {
        try
        {
            Process diskPartProcess = new Process();
            diskPartProcess.StartInfo.FileName = "diskpart";
            diskPartProcess.StartInfo.Arguments = $"/s \"{scriptFilePath}\"";  // Pass the script file to diskpart
            diskPartProcess.StartInfo.RedirectStandardOutput = true;
            diskPartProcess.StartInfo.UseShellExecute = false;
            diskPartProcess.StartInfo.CreateNoWindow = true;

            // Start the process and wait for it to finish
            diskPartProcess.Start();
            diskPartProcess.WaitForExit();

            // Output the results (optional)
            string output = diskPartProcess.StandardOutput.ReadToEnd();
            Console.WriteLine(output);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running DiskPart: {ex.Message}");
        }
    }
}
