using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EuroScope_Setup.Helpers
{
    class ConfigHelper
    {
        public static Dictionary<string, object> ParseConfigFile(string filePath)
        {
            var config = new Dictionary<string, object>();

            foreach (string line in File.ReadAllLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line) || !line.Contains("="))
                    continue;

                string[] parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim('"').Trim();
                    string value = parts[1].Trim('"').Trim();

                    if (value.Contains(";"))
                    {
                        string[] items = value.Split(';', StringSplitOptions.RemoveEmptyEntries);
                        config[key] = items.Select(item => item.Trim()).ToArray();
                    }
                    else
                    {
                        config[key] = value;
                    }
                }
            }

            return config;
        }


        public static void SaveConfigFile(string filePath, Dictionary<string, object> config)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                foreach (var kvp in config)
                {
                    if (kvp.Value is string stringValue)
                    {
                        writer.WriteLine($"{kvp.Key}=\"{stringValue}\"");
                    }
                    else if (kvp.Value is string[] arrayValue && arrayValue.Length > 0)
                    {
                        string joinedValue = string.Join(";", arrayValue);
                        writer.WriteLine($"{kvp.Key}=\"{joinedValue}\"");
                    }
                }
            }
        }
    }
}
