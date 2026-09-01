using System.Collections.Generic;

namespace CADSimulator.Models
{
    public class Component
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Pose Pose { get; set; } = new Pose();
        public List<Component> Children { get; set; } = new List<Component>();

        /// <summary>Analytic (plane/cylinder) face geometry read from this component's BREP solid, used as constraint targets.</summary>
        public List<FaceGeometry> Faces { get; set; } = new List<FaceGeometry>();
    }
}
