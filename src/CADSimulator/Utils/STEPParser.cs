using System;
using CADSimulator.Models;

namespace CADSimulator.Utils
{
    /// <summary>
    /// Parses STEP/STP files into an Assembly tree. Actual geometry decoding requires an
    /// Open CASCADE binding (e.g. OccSharp) which is not wired up yet in this scaffold.
    /// </summary>
    public static class STEPParser
    {
        public static Assembly Parse(string filePath)
        {
            throw new NotImplementedException(
                "STEP parsing requires an Open CASCADE binding (e.g. OccSharp). " +
                "Wire it up here to walk the STEP product structure into Assembly/Component nodes.");
        }
    }
}
