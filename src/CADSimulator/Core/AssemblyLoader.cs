using CADSimulator.Models;
using CADSimulator.Utils;

namespace CADSimulator.Core
{
    public static class AssemblyLoader
    {
        public static Assembly LoadFromStep(string filePath)
        {
            var assembly = STEPParser.Parse(filePath);
            assembly.SourceFilePath = filePath;
            return assembly;
        }
    }
}
