using System.Collections.Generic;

namespace CADSimulator.Models
{
    public class Component
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Pose Pose { get; set; } = new Pose();
        public List<Component> Children { get; set; } = new List<Component>();

        /// <summary>Path to the source geometry for this node (e.g. a shape within the loaded STEP file).</summary>
        public string? GeometryRef { get; set; }
    }
}
