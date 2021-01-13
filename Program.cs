namespace PrerequisiteInstaller
{
    using System;               //Console, Exception, ArgumentNullException, DateTime
    using System.Diagnostics;   //Process
    using System.IO;            //File, Directory, StreamWriter
    using System.Linq;          //Count
    using System.Net;           //WebClient
    using System.Security;      //SecurityException
    using System.Text.Json;     //duh
    using Microsoft.Win32;      //Registry (Only Microsoft.Win32.Registry is included in ItemGroup, see .csproj)

    public class InstallPrerequisites
    {
        private static void Log(string logText)
        {
            try
            {
                //Write to log
                using (StreamWriter w = File.AppendText("aiopi.log"))
                {
                    w.WriteLine(logText);
                }
            }

            catch (Exception e)
            {
                PrintError(e, "Couldn't write to log!", true);
            }
        }

        private static void PrintError(Exception e, string errorMessage, bool fatal)
        {
            string enterAction = "continue";
            string logPrefix = "";

            //Print [FATAL] tag
            if (fatal)
            {
                enterAction = "quit";
                Console.Write("[FATAL]");
                logPrefix = "[FATAL]";
            }

            //Print [ERROR] and message
            Console.WriteLine("[ERROR] " + errorMessage);
            Console.WriteLine(e.Message);
            Console.WriteLine("\nPress Enter to " + enterAction + "...");

            //Log error
            if (errorMessage != "Couldn't write to log!")
                Log(String.Concat(logPrefix, "[ERROR] " + errorMessage + " (" + e.Message + ")"));

            //Press Enter to continue or quit
            Console.ReadLine();
            if (errorMessage != "Couldn't write to log!")
                Log("[USER] User pressed Enter to " + enterAction);
            if (fatal)
                System.Environment.Exit(1);
        }

        private static bool CheckRegistry(string subKey, string value)
        {
            //Retrieve registry key as read-only
            try
            {
                RegistryKey sk = Registry.LocalMachine.OpenSubKey(subKey);
                return (sk.GetValue(value).ToString() == "1");
            }

            catch(Exception e)
            {
                //Key value is null
                if (e is ArgumentNullException)
                    return false;

                else
                {
                    //Write additional information if error is caused by security
                    string errorMessage = "Couldn't read registry key " + subKey + "!";
                    if (e is SecurityException)
                        errorMessage += " Try running this program as an administrator.";
                    PrintError(e, errorMessage, false);
                }
            }

            return false;
        }

        private static bool CheckDirectory(string directoryPath)
        //This method will result in some false-positives, but honestly, how many people don't have their OS on the C drive?
        {
            return Directory.Exists(directoryPath);
        }

        public static void Main(string[] args)
        {
            string version = "0.3.2";
            
            if (CheckRegistry(@"SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App", "3.1.10"))
                Console.WriteLine("all good");

            if (CheckDirectory("C:\\Program Files\\dotnet\\shared\\Microsoft.WindowsDesktop.App\\5.0.1"))
                Console.WriteLine("double all good");

            //Reset log
            if (File.Exists("aiopi.log"))
                try
                {
                    File.Delete("aiopi.log");
                }

                catch (Exception e)
                {
                    PrintError(e, "Couldn't reset log file!", true);
                }

            //Create new log
            Log("All-In-One Prerequisite Installer v" + version + "\n[INFO] Created new log on " + DateTime.Now);

            //Set up variables
            string[] fileURLs = new string[] {
                "https://aka.ms/vs/16/release/VC_redist.x64.exe",
                "https://aka.ms/vs/16/release/VC_redist.x86.exe",
                "https://download.visualstudio.microsoft.com/download/pr/9845b4b0-fb52-48b6-83cf-4c431558c29b/41025de7a76639eeff102410e7015214/dotnet-runtime-3.1.10-win-x64.exe",
                "https://download.visualstudio.microsoft.com/download/pr/c6a74d6b-576c-4ab0-bf55-d46d45610730/f70d2252c9f452c2eb679b8041846466/windowsdesktop-runtime-5.0.1-win-x64.exe",
                "https://download.visualstudio.microsoft.com/download/pr/55bb1094-db40-411d-8a37-21186e9495ef/1a045e29541b7516527728b973f0fdef/windowsdesktop-runtime-5.0.1-win-x86.exe"};

            int fileCount = fileURLs.Count();
            int downloadSuccessCount = 0;
            int installSuccessCount = 0;
            Process myProcess = new Process();

            //Intro
            Console.WriteLine("P4G PC All-in-One Prerequisite Installer v" + version + "\nby Pixelguin\n");
            Console.WriteLine("This program will download and run " + fileCount + " installers, one after the other.\nJust follow the instructions in this window.\n\nPress Enter to start.");
            
            Console.ReadLine();
            Log("[USER] User pressed Enter to start downloading");

            //Create directory
            if (!Directory.Exists("aiopi_downloads"))
            {
                Directory.CreateDirectory("aiopi_downloads");
                Console.WriteLine("Created aiopi_downloads directory. Don't delete this! The program will delete the directory automatically at the end.");
                Log("[INFO] Created aiopi_downloads directory");
            }

            //Download files
            using (var client = new WebClient())
            {
                for (var i = 0; i < fileURLs.Count(); i++)
                {
                    //Set file name
                    string fileName = System.IO.Path.GetFileName(fileURLs[i]);

                    try
                    {
                        string downloadText = "Downloading " + fileName + " (" + (i + 1) + "/" + fileCount + ")";

                        //Output to log and console
                        Console.Write(downloadText + "...");
                        Log("[INFO] " + downloadText + " from " + fileURLs[i]);

                        //Download the file
                        client.DownloadFile(fileURLs[i], @"aiopi_downloads\" + fileName);

                        Console.WriteLine("Done!");
                        Log("[INFO] Download successful, downloadSuccessCount = " + ++downloadSuccessCount);
                    }

                    catch (Exception e)
                    {
                        PrintError(e, "Couldn't download " + fileName + "!", false);
                    }
                }
            }

            //Create array of files in downloads directory
            string[] files = Directory.GetFiles("aiopi_downloads");

            //Install files
            for (var i = 0; i < files.Count(); i++)
            {
                var fileName = files[i];

                try
                {
                    //Output to console and log
                    Console.Clear();
                    string launchText = "Launching " + fileName + " (" + (i + 1) + "/" + fileCount + ")";

                    Console.WriteLine(new string('=', launchText.Length + 3));
                    Console.WriteLine(launchText + "...");
                    Console.WriteLine(new string('=', launchText.Length + 3));
                    Log("[INFO] " + launchText);
                    
                    /*String for options that appear if the user has already installed the program + next steps to take
                      Not currently very interesting but may be useful in the future if files change */
                    string alreadyInstalledButtons = "Repair/Uninstall/Close";
                    string alreadyInstalledActions = "Close => Yes";

                    Console.WriteLine("\nIf the options given to you are " + alreadyInstalledButtons + ", you already have this installed.\nClick " + alreadyInstalledActions + " and the program will continue.");
                    Console.WriteLine("\nIf you see something else, follow the installer's instructions.");

                    //Launch installer and wait for it to close
                    myProcess.StartInfo.FileName = fileName;
                    myProcess.Start();
                    myProcess.WaitForExit();
                    Log("[INFO] Run successful, installSuccessCount = " + ++installSuccessCount);
                }

                catch (Exception e)
                {
                    PrintError(e, "Couldn't run " + fileName + "!", false);
                }
            }

            //Wrap-up output to log and console
            Console.Clear();

            string outputText = "Successfully downloaded " + downloadSuccessCount + "/" + fileCount + " files!";
            Console.WriteLine(outputText);
            Log("[INFO] " + outputText);

            outputText = "Successfully ran " + installSuccessCount + "/" + fileCount + " installers!";
            Console.WriteLine(outputText);
            Log("[INFO] " + outputText);

            //Delete downloads directory
            try
            {
                Directory.Delete("aiopi_downloads", true);
                Console.WriteLine("Successfully deleted aiopi_downloads directory!");
                Log("[INFO] Deleted aiopi_downloads directory");
            }

            catch (Exception e)
            {
                PrintError(e, "Couldn't delete aiopi_downloads directory!", false);
            }

            Console.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();
            Log("[USER] User pressed Enter to exit");
        }
    }
}
