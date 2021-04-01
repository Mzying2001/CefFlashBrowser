using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CefFlashBrowser.Services
{
    class JsonDataService
    {
        public static JObject ReadFile(string fileName)
        {
            var json = new JObject();

            if (!File.Exists(fileName))
                return json;

            using (var reader = File.OpenText(fileName))
            using (var jsreader = new JsonTextReader(reader))
                json = (JObject)JToken.ReadFrom(jsreader);

            return json;
        }

        public static void WriteFile(string fileName, JObject json)
        {
            string dir = fileName.Substring(0, fileName.LastIndexOf('\\'));
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fileName, json.ToString());
        }
    }
}
