using System.Collections.Generic;

namespace CADSimulator.Models
{
    public class Frame
    {
        /// <summary>Time offset in milliseconds from the start of the sequence.</summary>
        public double Time { get; set; }
        public Dictionary<string, Pose> Poses { get; set; } = new Dictionary<string, Pose>();
    }

    public class SequenceEvent
    {
        public double Time { get; set; }
        public string Action { get; set; } = string.Empty;
        public object? Value { get; set; }
    }

    public class Sequence
    {
        public string Name { get; set; } = string.Empty;
        public List<Frame> Frames { get; set; } = new List<Frame>();
        public List<SequenceEvent> Events { get; set; } = new List<SequenceEvent>();
    }
}
