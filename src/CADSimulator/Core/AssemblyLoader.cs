using CADSimulator.Models;

namespace CADSimulator.Core
{
    public static class AssemblyLoader
    {
        public static Assembly LoadFromStep(string filePath)
        {
            var assembly = StepAssemblyReader.Read(filePath);
            assembly.SourceFilePath = filePath;
            return assembly;
        }
    }
}
