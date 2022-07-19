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
using System.Text.RegularExpressions;
using System.Linq;
using System;
using System.IO;
using System.Net;
using System.Net.Mime;



public class HttpWebRequestDownload
{
    private long _totalBytesLength = 0;
    private long _transferredBytes = 0;
    private int _transferredPercents => (int)((100 * _transferredBytes) / _totalBytesLength);
    private string _defaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    public string downloadedFilePath = String.Empty;

    public HttpWebRequestDownload()
    {
        //Windows 7 fix
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
    }
    public static void drawTextProgressBar(string stepDescription, int progress, int total)
    {
        int totalChunks = 50;

        //draw empty progress bar
        Console.CursorLeft = 0;
        Console.Write("["); //start
        Console.CursorLeft = totalChunks + 1;
        Console.Write("]"); //end
        Console.CursorLeft = 1;

        double pctComplete = Convert.ToDouble(progress) / total;
        int numChunksComplete = Convert.ToInt16(totalChunks * pctComplete);

        //draw completed chunks
        Console.BackgroundColor = ConsoleColor.Green;
        Console.Write("".PadRight(numChunksComplete));

        //draw incomplete chunks
        Console.BackgroundColor = ConsoleColor.Gray;
        Console.Write("".PadRight(totalChunks - numChunksComplete));

        //draw totals
        Console.CursorLeft = totalChunks + 5;
        Console.BackgroundColor = ConsoleColor.Black;

        string output = progress.ToString() + "/" + total.ToString();
        Console.Write(output.PadRight(5) + stepDescription); //pad the output so when changing from 3 to 4 digits we avoid text shifting
    }
    public void DownloadFile(string url, string destinationDirectory = default)
    {
        string filename = "";
        if (destinationDirectory == default)
            destinationDirectory = _defaultDirectory;

        try
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.AllowAutoRedirect = true;
            request.Headers.Add("Cache-Control", "no-cache");
            request.Headers.Add("Cache-Control", "no-store");
            request.Headers.Add("Cache-Control", "max-age=1");
            request.Headers.Add("Cache-Control", "s-maxage=1");
            request.Headers.Add("Pragma", "no-cache");
            request.Headers.Add("Expires", "-1");

            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponseAsync().Result)
            {
                _totalBytesLength = response.ContentLength;

                string path = response.Headers["Content-Disposition"];
                if (string.IsNullOrWhiteSpace(path))
                {
                    var uri = new Uri(url);
                    filename = Path.GetFileName(uri.LocalPath);
                }
                else
                {
                    ContentDisposition contentDisposition = new ContentDisposition(path);
                    filename = contentDisposition.FileName;
                }

                using (Stream responseStream = response.GetResponseStream())
                using (FileStream fileStream = File.Create(System.IO.Path.Combine(destinationDirectory, filename)))
                {
                    byte[] buffer = new byte[1024 * 1024]; // 1MB buffer
                    ProgressEventArgs eventArgs = new ProgressEventArgs(_totalBytesLength);

                    int size = responseStream.Read(buffer, 0, buffer.Length);
                    while (size > 0)
                    {
                        fileStream.Write(buffer, 0, size);
                        _transferredBytes += size;

                        size = responseStream.Read(buffer, 0, buffer.Length);

                        eventArgs.UpdateData(_transferredBytes, _transferredPercents);
                        OnDownloadProgressChanged(eventArgs);
                    }
                }
            }

            downloadedFilePath = Path.Combine(destinationDirectory, filename);
            OnDownloadFileCompleted(EventArgs.Empty);
        }
        catch (Exception e)
        {
            OnError($"{e.Message} => {e?.InnerException?.Message}");
        }
    }

    //events
    public event EventHandler<ProgressEventArgs> DownloadProgressChanged;
    public event EventHandler DownloadFileCompleted;
    public event EventHandler<string> Error;

    public class ProgressEventArgs : EventArgs
    {
        public long TransferredBytes { get; set; }
        public int TransferredPercents { get; set; }
        public long TotalBytesLength { get; set; }

        public ProgressEventArgs(long transferredBytes, int transferredPercents, long totalBytesLength)
        {
            TransferredBytes = transferredBytes;
            TransferredPercents = transferredPercents;
            TotalBytesLength = totalBytesLength;
        }

        public ProgressEventArgs(long totalBytesLength)
        {
            this.TotalBytesLength = totalBytesLength;
        }

        public void UpdateData(long transferredBytes, int transferredPercents)
        {
            TransferredBytes = transferredBytes;
            TransferredPercents = transferredPercents;
        }
    }

    protected virtual void OnDownloadProgressChanged(ProgressEventArgs e)
    {
        DownloadProgressChanged?.Invoke(this, e);
    }
    protected virtual void OnDownloadFileCompleted(EventArgs e)
    {
        DownloadFileCompleted?.Invoke(this, e);
    }
    protected virtual void OnError(string errorMessage)
    {
        Error?.Invoke(this, errorMessage);
    }
    
    class Program
    {
        

        public static Config config = Config.GetConfig(); // ????????? what the fuck????????
        static bool downloadComplete = false;
        public static int downloadPercentage = 0;
        [STAThread]
        static void Main(string[] args)
        {
            //Console.WindowHeight /= 2;
            //Console.WindowWidth /= 2;

            Console.BufferHeight = Console.WindowHeight;
            Console.BufferWidth = Console.WindowWidth;

            bool hasSelectedFile = args.Length > 0;
            // LOAD CONFIG

            Console.WriteLine(@"-----------------------
| 北极星CN一键安装启动器 |
-----------------------");

            if (config.installPath == "")
            {
                config.installPath = InstallPath.GetInstallPath();
            }

            bool hasNorthstarInstalled = File.Exists(Path.Combine(config.installPath, "NorthstarLauncher.exe"));

            Console.WriteLine($"定位到泰坦陨落2安装目录: {config.installPath}");

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
                    Console.WriteLine("错误:ZIP文件不存在,请尝试重新启动本程序!");
                    Console.ReadLine();
                    return;
                }
                if (Path.GetExtension(path) != ".zip")
                {
                    Console.WriteLine("错误:非法的ZIP文件,请尝试重新启动本程序!");
                    Console.ReadLine();
                    return;
                }
                if (!hasNorthstarInstalled)
                {
                    Console.WriteLine("北极星CN未安装!");
                    Console.ReadLine();
                    return;
                }

                string dirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileNameWithoutExtension(path));
                Directory.CreateDirectory(dirPath);
                // extract mod
                if (Directory.Exists(dirPath))
                    Directory.Delete(dirPath, true);

                ZipFile.ExtractToDirectory(path, dirPath);

                string modJson = FindFileInDirectory(dirPath, "mod.json");

                if (modJson == "")
                {
                    Console.WriteLine("无法在zip包内查找mod.json!");
                    Directory.Delete(dirPath, true);
                    Console.ReadLine();
                    return;
                }

                Console.WriteLine(modJson);
                string dirToCopy = Path.GetDirectoryName(modJson);

                Console.WriteLine($"dirToCopy: {dirToCopy}");

                CopyDirectory(dirToCopy, Path.Combine(config.installPath, "R2Northstar/mods/", Path.GetFileName(dirToCopy)));

                Directory.Delete(dirPath, true);

                Console.Clear();
                Console.WriteLine($@"-----------------------
| 北极星CN一键安装启动器 |
-----------------------
Mod {Path.GetFileName(dirToCopy)} 安装成功!");
            }

            Console.WriteLine("正在启动北极星CN...");

            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"), JsonConvert.SerializeObject(config));

            Console.WriteLine(@"--------------------
| 北极星CN正在运行 |
--------------------

请不要关闭此程序!我们将会在游戏退出时保存您的设置,以便下次加速启动!");

            string settingsFilePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            settingsFilePath = Path.Combine(settingsFilePath, "Respawn/Titanfall2/profile/profile.cfg");

            if (File.Exists(settingsFilePath) && File.Exists("./profile.cfg"))
            {
                string newProfileCFG = "";
                string[] settings = File.ReadAllText(settingsFilePath).Split('\n');
                string[] backup = File.ReadAllText("./profile.cfg").Split('\n');

                for (int i = 0; i < backup.Length; i++)
                {
                    int j = 0;
                    for (j = 0; j < settings.Length; j++)
                    {
                        if (settings[j].StartsWith(backup[i].Split(' ')[0]))
                            break;
                    }

                    if (j == settings.Length)
                        newProfileCFG += backup[i] + "\n";
                    else newProfileCFG += settings[j] + "\n";
                }
                File.WriteAllText(settingsFilePath, newProfileCFG);
            }


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
        
        static dynamic GetRemoteVersion(string uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                string jsonreply = reader.ReadToEnd();
                dynamic reply = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonreply);
                return reply;
            }
        }
        //PROGRESS BAR HERE
        //PROGRESS BAR HERE
        //PROGRESS BAR HERE
        //PROGRESS BAR HERE
        //PROGRESS BAR HERE
        static void HDownloadOnDownloadProgressChanged(object sender, HttpWebRequestDownload.ProgressEventArgs e)
        {
            drawTextProgressBar("MB ("+ e.TransferredPercents + "%)", (int)Math.Round((double)e.TransferredBytes/1024000),(int)Math.Round((double)e.TotalBytesLength/1024000));
        }
        static string CheckForUpdates(string curNorthstarVersion)
        {
            // get current latest release version!
            // try/catch in case there is no internet!
            //IReadOnlyList<Octokit.Release> releases = GetReleases();
            //Get release string here

            dynamic release = GetRemoteVersion("https://nscn.wolf109909.top/version/query");

            // install / update outdated northstar
            if (release != null && (release.tag_name != curNorthstarVersion))
            {
                //string downloadlink;
                string filename = release.tag_name + ".zip";
                string downloadlinkjson = release.assets[0].browser_download_url;
                string downloadlink = downloadlinkjson.Replace("{", string.Empty).Replace("}", string.Empty);
                string downloadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
                //string directdownloadlink = ParseRedirectDownloadLink(new Uri(downloadlink));
                
                HttpWebRequestDownload hDownload = new HttpWebRequestDownload();
                Console.WriteLine("\n");
                hDownload.DownloadProgressChanged += HDownloadOnDownloadProgressChanged;
                hDownload.DownloadFileCompleted += delegate (object o, EventArgs args)
                {
                    Console.WriteLine("\n");
                    Console.WriteLine("下载完成,临时文件保存至: " + hDownload.downloadedFilePath);
                };
                hDownload.Error += delegate (object o, string errMessage) { Debug.WriteLine("发生错误,请重试 !! => " + errMessage); };
                hDownload.DownloadFile(downloadlink, AppDomain.CurrentDomain.BaseDirectory);


                Console.WriteLine(@"-----------------------
| 北极星CN一键安装启动器 |
-----------------------
正在安装北极星CN...");
                if (Directory.Exists("./Northstar"))
                {
                    Console.WriteLine("发现残留缓存文件，正在清理...");
                    Directory.Delete("./Northstar", true);
                }

                ZipFile.ExtractToDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename), "./Northstar");
                PreInstallProcedures();
                Console.WriteLine("正在复制文件到游戏目录...");

                CopyDirectory("./Northstar", config.installPath);

                Console.WriteLine("正在删除缓存...");

                Directory.Delete("./Northstar", true);
                File.Delete(downloadPath);

                curNorthstarVersion = release.tag_name;
                Console.WriteLine("北极星CN安装完成!");
            }
            return curNorthstarVersion;
        }
        static void PreInstallProcedures() 
        {
            Console.WriteLine("正在准备安装环境...");
            if (Directory.Exists(config.installPath+ "/R2Northstar/mods/Northstar.Client"))
            {
                File.WriteAllText("./Northstar/R2Northstar/mods/Northstar.Client/mod/cfg/autoexec_ns_client.cfg", File.ReadAllText(config.installPath + "/R2Northstar/mods/Northstar.Client/mod/cfg/autoexec_ns_client.cfg"));
                Directory.Delete(config.installPath + "/R2Northstar/mods/Northstar.Client", true);
            }
            if (Directory.Exists(config.installPath + "/R2Northstar/mods/Northstar.CustomServers"))
            {
                File.WriteAllText("./Northstar/R2Northstar/mods/Northstar.CustomServers/mod/cfg/autoexec_ns_server.cfg", File.ReadAllText(config.installPath + "/R2Northstar/mods/Northstar.CustomServers/mod/cfg/autoexec_ns_server.cfg"));
                Directory.Delete(config.installPath + "/R2Northstar/mods/Northstar.CustomServers", true);
            }
            if (Directory.Exists(config.installPath + "/R2Northstar/mods/Northstar.Custom"))
            {
                Directory.Delete(config.installPath + "/R2Northstar/mods/Northstar.Custom", true);
            }
            if (Directory.Exists(config.installPath + "/R2Northstar/mods/NorthstarCN.Custom"))
            {
                Directory.Delete(config.installPath + "/R2Northstar/mods/NorthstarCN.Custom", true);
            }

            if (File.Exists(config.installPath+ "/ns_startup_args.txt")) 
            {
                File.WriteAllText("./Northstar/ns_startup_args.txt", File.ReadAllText(config.installPath + "/ns_startup_args.txt"));
            }
            if (File.Exists(config.installPath + "/ns_startup_args_dedi.txt"))
            {
                File.WriteAllText("./Northstar/ns_startup_args_dedi.txt", File.ReadAllText(config.installPath + "/ns_startup_args_dedi.txt"));
            }
            //Directory.Delete(config.installPath + "/R2Northstar/mods/Northstar.Client", true);
            //Directory.Delete(config.installPath + "/R2Northstar/mods/Northstar.CustomServers", true);
            //Directory.Delete(config.installPath + "/R2Northstar/mods/Northstar.Custom", true);
            //Directory.Delete(config.installPath + "/R2Northstar/mods/NorthstarCN.Custom", true);


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
                throw new DirectoryNotFoundException($"无法找到源文件夹: {dir.FullName}");

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


        static string FindFileInDirectory(string sourceDir, string fileName, bool recursive = true)
        {
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                Console.WriteLine($"{fileName} == {file}");
                if (Path.Combine(sourceDir, fileName) == file)
                {
                    Console.WriteLine(Path.Combine(sourceDir, file));
                    return Path.Combine(sourceDir, file);
                }
            }

            if (recursive)
                foreach (string dir in Directory.GetDirectories(sourceDir))
                {
                    string res = FindFileInDirectory(dir, fileName);
                    if (res != "") return res;
                }

            return "";
        }
    }
}
