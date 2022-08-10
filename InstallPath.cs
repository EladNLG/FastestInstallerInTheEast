using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

class InstallPath
{
    public static string GetInstallPath()
    {
        string path = "C:/ProgramData/Microsoft/Windows/Start Menu/Programs/Steam";
        
        if (!Directory.Exists(path) || !File.Exists(Path.Combine(path, "Steam.lnk")))
        {
            if (Directory.Exists("C:/Program Files (x86)/Origin Games/Titanfall2") && File.Exists("C:/Program Files (x86)/Origin Games/Titanfall2/Titanfall2.exe"))
                return "C:/Program Files (x86)/Origin Games/Titanfall2";

            try
            {
                RegistryKey originReg = Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Respawn").OpenSubKey("Titanfall2");
                if (originReg.GetValue("Install Dir") != null) return (string)originReg.GetValue("Install Dir");
            }
            catch
            {

            }

            MessageBox.Show("无法自动寻找游戏安装目录", "自动获取游戏安装目录失败,请手动选择《泰坦陨落2》游戏安装目录。", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return GetInstallPathManually();
        }

        // AUTOMATIC AQCUISITION
        // THE COOL SHIT:TM:

        string target = GetShortcutTarget(Path.Combine(path, "Steam.lnk"));
        string steamDir = Path.GetDirectoryName(target);

        Console.WriteLine(target);

        List<string> folderPaths = new List<string>();

        // probably stupid, but meh
        string[] libraryFolders = File.ReadAllText(Path.Combine(steamDir, "config/libraryfolders.vdf")).Split('\"');
        for (int i = 0; i < libraryFolders.Length; i++)
        {
            string val = libraryFolders[i];
            if (val == "path")
            {
                Console.WriteLine(libraryFolders[i + 2]);
                folderPaths.Add(libraryFolders[i + 2]);
            }
        }

        foreach (string folder in folderPaths)
        {
            //Console.WriteLine(folder);
            if (!Directory.Exists(folder))
            {
                Console.WriteLine("文件系统中不存在 " + folder);
                continue;
            }
                
            
            Thread.Sleep(1000);
            try { 
                foreach (string dir in Directory.GetDirectories(Path.Combine(folder, "steamapps/common")))
                {
                    //Console.WriteLine(dir);
                    if (dir.EndsWith("Titanfall2") && File.Exists(Path.Combine(dir, "Titanfall2.exe")))
                    {
                        return dir;
                    }
                }
            }
            catch(DirectoryNotFoundException)
            {
                Console.WriteLine("文件系统中不存在" + folder);
            }
        }

        try
        {
            RegistryKey originReg = Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Respawn").OpenSubKey("Titanfall2");
            if (originReg.GetValue("Install Dir") != null) return (string)originReg.GetValue("Install Dir");
        }
        catch
        {

        }

        if (Directory.Exists("C:/Program Files (x86)/Origin Games/Titanfall2") && File.Exists("C:/Program Files (x86)/Origin Games/Titanfall2/Titanfall2.exe"))
            return "C:/Program Files (x86)/Origin Games/Titanfall2";
        MessageBox.Show("无法自动寻找游戏安装目录", "自动获取游戏安装目录失败,请手动选择《泰坦陨落2》游戏安装目录。", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        return GetInstallPathManually();
    }

    static string GetInstallPathManually()
    {
        OpenFileDialog dialog = new OpenFileDialog();
        dialog.Title = "选择《泰坦陨落2》游戏安装目录";
        dialog.Multiselect = false;
        dialog.InitialDirectory = "C:\\Program Files (x86)\\Steam";
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            MessageBox.Show("错误!", "选择的目录下不存在《泰坦陨落2》主程序文件!请选择游戏安装根目录!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return GetInstallPathManually();
        }
        return Path.GetDirectoryName(dialog.FileName);
    }
    
    // stolen from the internet
    private static string GetShortcutTarget(string file)
    {
        try
        {
            if (System.IO.Path.GetExtension(file).ToLower() != ".lnk")
            {
                throw new Exception("Supplied file must be a .LNK file");
            }

            FileStream fileStream = File.Open(file, FileMode.Open, FileAccess.Read);
            using (System.IO.BinaryReader fileReader = new BinaryReader(fileStream))
            {
                fileStream.Seek(0x14, SeekOrigin.Begin);     // Seek to flags
                uint flags = fileReader.ReadUInt32();        // Read flags
                if ((flags & 1) == 1)
                {                      // Bit 1 set means we have to
                                       // skip the shell item ID list
                    fileStream.Seek(0x4c, SeekOrigin.Begin); // Seek to the end of the header
                    uint offset = fileReader.ReadUInt16();   // Read the length of the Shell item ID list
                    fileStream.Seek(offset, SeekOrigin.Current); // Seek past it (to the file locator info)
                }

                long fileInfoStartsAt = fileStream.Position; // Store the offset where the file info
                                                             // structure begins
                uint totalStructLength = fileReader.ReadUInt32(); // read the length of the whole struct
                fileStream.Seek(0xc, SeekOrigin.Current); // seek to offset to base pathname
                uint fileOffset = fileReader.ReadUInt32(); // read offset to base pathname
                                                           // the offset is from the beginning of the file info struct (fileInfoStartsAt)
                fileStream.Seek((fileInfoStartsAt + fileOffset), SeekOrigin.Begin); // Seek to beginning of
                                                                                    // base pathname (target)
                long pathLength = (totalStructLength + fileInfoStartsAt) - fileStream.Position - 2; // read
                                                                                                    // the base pathname. I don't need the 2 terminating nulls.
                char[] linkTarget = fileReader.ReadChars((int)pathLength); // should be unicode safe
                var link = new string(linkTarget);

                int begin = link.IndexOf("\0\0");
                if (begin > -1)
                {
                    int end = link.IndexOf("\\\\", begin + 2) + 2;
                    end = link.IndexOf('\0', end) + 1;

                    string firstPart = link.Substring(0, begin);
                    string secondPart = link.Substring(end);

                    return firstPart + secondPart;
                }
                else
                {
                    return link;
                }
            }
        }
        catch
        {
            return "";
        }
    }
}
