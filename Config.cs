using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

public class Config
{
    public string installPath = "";
    public bool enableAutoUpdates = true;

    public static Config GetConfig()
    {
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        if (!File.Exists(configPath))
            return new Config();

        return JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));
    }
}
