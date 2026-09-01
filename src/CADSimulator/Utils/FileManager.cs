using System.IO;
using CADSimulator.Models;
using Newtonsoft.Json;

namespace CADSimulator.Utils
{
    /// <summary>Handles saving/loading sequences and project metadata as JSON (see Projects/ folder).</summary>
    public static class FileManager
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };

        public static void SaveSequence(Sequence sequence, string filePath)
        {
            var json = JsonConvert.SerializeObject(sequence, Settings);
            File.WriteAllText(filePath, json);
        }

        public static Sequence LoadSequence(string filePath)
        {
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<Sequence>(json, Settings)
                   ?? new Sequence();
        }
    }
}
