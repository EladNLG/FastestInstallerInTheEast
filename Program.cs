using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.IO.Compression;
using Newtonsoft.Json;
using System.Diagnostics;
using System.ComponentModel;
using Microsoft.Win32;

class Program
{
    public static Config config = Config.GetConfig(); // ????????? what the fuck????????
    static bool downloadComplete = false;
    public static int downloadPercentage = 0;
    [STAThread]
    static void Main(string[] args)
    {
        Console.WindowHeight /= 2;
        Console.WindowWidth /= 2;
        Console.BufferHeight = Console.WindowHeight;
        Console.BufferWidth = Console.WindowWidth;

        bool hasSelectedFile = args.Length > 0;
        // LOAD CONFIG

        Console.WriteLine(@"---------
| FIITE |
---------");

        if (config.installPath == "")
        {
            config.installPath = InstallPath.GetInstallPath();
        }

        bool hasNorthstarInstalled = File.Exists(Path.Combine(config.installPath, "NorthstarLauncher.exe"));

        Console.WriteLine($"TITANFALL 2 FOLDER IS: {config.installPath}");

        string northstarVersion = "";
        if (hasNorthstarInstalled)
        {
            using (JsonTextReader json = new JsonTextReader(new StringReader(File.ReadAllText(Path.Combine(config.installPath, "R2Northstar/mods/Northstar.Custom/mod.json")))))
            {
                while (json.Read())
                {
                    if (json.Path == "Version")
                    {
                        northstarVersion = "v" + (string)json.Value;
                    }
                }
            }
        }

        if (config.enableAutoUpdates)
            CheckForUpdates(northstarVersion);

        if (hasSelectedFile)
        {
            string path = args[0];

            // check if zip file is valid
            if (!File.Exists(path))
            {
                Console.WriteLine("FILE DOES NOT EXIST!");
                Console.ReadLine();
                return;
            }
            if (Path.GetExtension(path) != ".zip")
            {
                Console.WriteLine("FILE SELECTED IS NOT A ZIP FILE!");
                Console.ReadLine();
                return;
            }
            if (!hasNorthstarInstalled)
            {
                Console.WriteLine("NORTHSTAR IS NOT INSTALLED!");
                Console.ReadLine();
                return;
            }

            string dirPath = Path.Combine(".", Path.GetFileNameWithoutExtension(path));
            // extract mod
            ZipFile.ExtractToDirectory(path, dirPath);

            string dirToCopy = Path.GetDirectoryName(FindFileInDirectory(dirPath, "mod.json"));

            Console.WriteLine($"dirToCopy: {dirToCopy}");

            CopyDirectory(dirToCopy, Path.Combine(config.installPath, "R2Northstar/mods/", Path.GetFileName(dirPath)));

            Directory.Delete(dirToCopy, true);
        }

        Console.WriteLine("Booting up northstar...");

        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configg.json"), JsonConvert.SerializeObject(config));

        Console.Clear();

        Console.WriteLine(@"--------------------
| NORTHSTAR ACTIVE |
--------------------

Do not close me! I will save your settings so they don't get reset when booting up vanilla!");

        string settingsFilePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        settingsFilePath = Path.Combine(settingsFilePath, "Respawn/Titanfall2/profile/profile.cfg");

        if (File.Exists(settingsFilePath) && File.Exists("./profile.cfg"))
            File.Copy("./profile.cfg", settingsFilePath, true);

        ProcessStartInfo procStartInfo = new ProcessStartInfo();
        Process process = new Process();
        procStartInfo.FileName = Path.Combine(config.installPath, "NorthstarLauncher.exe");
        procStartInfo.WorkingDirectory = config.installPath;

        // procStartInfo.Arguments = args;

        process.StartInfo = procStartInfo;

        process.Start();
        int id = process.Id;
        Process tempProc = Process.GetProcessById(id);

        process.Close();

        tempProc.WaitForExit();

        File.Copy(settingsFilePath, "./profile.cfg", true);
    }

    static string CheckForUpdates(string curNorthstarVersion)
    {
        // get current latest release version!
        // try/catch in case there is no internet!
        IReadOnlyList<Octokit.Release> releases = GetReleases();

        // install / update outdated northstar
        if (releases != null && (releases[0].TagName != curNorthstarVersion))
        {
            string downloadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, releases[0].Assets[0].Name);
            using (WebClient client = new WebClient())
            {
                client.DownloadProgressChanged += DownloadProgressChanged;
                client.DownloadFileCompleted += DownloadComplete;
                client.DownloadFileAsync(new Uri(releases[0].Assets[0].BrowserDownloadUrl), downloadPath);
                int lastDownloadPercentage = 0;
                DateTime startTime = DateTime.Now;
                double downloadFrac = 0.0;
                double timeSinceStart = 0.0;
                double timeUntilComplete = 0.0;
                while (!downloadComplete)
                {
                    System.Windows.Forms.Application.DoEvents();
                    if (lastDownloadPercentage != downloadPercentage)
                    {
                        Console.Clear();
                        timeSinceStart = (DateTime.Now - startTime).TotalSeconds;
                        downloadFrac = (double)downloadPercentage / 100.0;
                        timeUntilComplete = (timeSinceStart / downloadFrac) - timeSinceStart;
                        // 
                        Console.WriteLine($@"---------
| FIITE |
---------
Downloading new Northstar version... {downloadPercentage}% ({timeUntilComplete.ToString("0")} seconds left)");
                        lastDownloadPercentage = downloadPercentage;
                    }
                }
                Console.Clear();
            }

            Console.WriteLine(@"---------
| FIITE |
---------
Extracting Northstar...");

            ZipFile.ExtractToDirectory(downloadPath, "./Northstar");

            Console.WriteLine("Copying to your Titanfall 2 folder...");

            CopyDirectory("./Northstar", config.installPath);

            Console.WriteLine("Deleting old files...");

            Directory.Delete("./Northstar", true);
            File.Delete(downloadPath);

            curNorthstarVersion = releases[0].TagName;
            Console.WriteLine("Northstar installed!");
        }
        return curNorthstarVersion;
    }
    static void DownloadComplete(object sender, AsyncCompletedEventArgs e)
    {
        downloadComplete = true;
    }
    static void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
        downloadPercentage = e.ProgressPercentage;
    }

    // util method - copy directory recursively.
    static void CopyDirectory(string sourceDir, string destinationDir, bool recursive = true)
    {
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        // Cache directories before we start copying
        DirectoryInfo[] dirs = dir.GetDirectories();

        // Create the destination directory
        Directory.CreateDirectory(destinationDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
    }

    static IReadOnlyList<Octokit.Release> GetReleases()
    {
        try
        {
            var github = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("GithubUpdater"));
            var task = github.Repository.Release.GetAll("R2Northstar", "Northstar");
            task.Wait();
            return task.Result;
        }
        catch
        {

        }
        return null;
    }

    static string FindFileInDirectory(string sourceDir, string fileName, bool recursive = true)
    {
        foreach (string file in Directory.GetFiles(sourceDir))
            if (fileName == file) return file;

        if (recursive)
            foreach (string dir in Directory.GetDirectories(sourceDir))
                FindFileInDirectory(dir, fileName);

        return "";
    }
}
