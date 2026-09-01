using System.Collections.Generic;
using System.Linq;

namespace CADSimulator.Utils.Step
{
    /// <summary>
    /// One keyword block of a STEP instance, e.g. "PRODUCT('Base','Base','',(#11))".
    /// A "complex" STEP instance (several supertypes sharing one id, written as
    /// "#N=(KEYWORD_A(...)KEYWORD_B(...));") is stored as multiple blocks on one StepEntity.
    /// </summary>
    public class StepEntityBlock
    {
        public string Keyword { get; set; } = string.Empty;
        public List<StepValue> Parameters { get; set; } = new List<StepValue>();
    }

    public class StepEntity
    {
        public int Id { get; set; }
        public List<StepEntityBlock> Blocks { get; set; } = new List<StepEntityBlock>();

        /// <summary>Keyword/Parameters of the first (usually only) block — convenient for simple instances.</summary>
        public string Keyword => Blocks.Count > 0 ? Blocks[0].Keyword : string.Empty;
        public List<StepValue> Parameters => Blocks.Count > 0 ? Blocks[0].Parameters : new List<StepValue>();

        public bool Is(string keyword) => Blocks.Any(b => b.Keyword == keyword);

        public StepEntityBlock? GetBlock(string keyword) => Blocks.FirstOrDefault(b => b.Keyword == keyword);
    }
}
