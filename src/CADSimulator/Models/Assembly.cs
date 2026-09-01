using System.Collections.Generic;

namespace CADSimulator.Models
{
    public class Assembly
    {
        public string Name { get; set; } = string.Empty;
        public string SourceFilePath { get; set; } = string.Empty;
        public List<Component> Components { get; set; } = new List<Component>();
        public List<Constraint> Constraints { get; set; } = new List<Constraint>();
        public List<Joint> Joints { get; set; } = new List<Joint>();

        public Component? FindComponent(string id)
        {
            foreach (var component in Components)
            {
                var found = FindComponentRecursive(component, id);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Component? FindComponentRecursive(Component current, string id)
        {
            if (current.Id == id)
            {
                return current;
            }

            foreach (var child in current.Children)
            {
                var found = FindComponentRecursive(child, id);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
