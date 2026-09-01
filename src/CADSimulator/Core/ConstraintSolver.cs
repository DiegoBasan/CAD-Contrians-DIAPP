using System;
using System.Collections.Generic;
using CADSimulator.Models;

namespace CADSimulator.Core
{
    /// <summary>
    /// Applies a set of constraints to an assembly, updating component poses so the
    /// constraints are satisfied. This scaffold only defines the entry point and
    /// per-type dispatch; the actual geometric resolution (e.g. iterative relaxation
    /// or a proper constraint graph solve) still needs to be implemented per type.
    /// </summary>
    public static class ConstraintSolver
    {
        public static void Solve(Assembly assembly, IEnumerable<Constraint> constraints)
        {
            foreach (var constraint in constraints)
            {
                ApplyConstraint(assembly, constraint);
            }
        }

        private static void ApplyConstraint(Assembly assembly, Constraint constraint)
        {
            var componentA = assembly.FindComponent(constraint.ComponentAId);
            var componentB = assembly.FindComponent(constraint.ComponentBId);

            if (componentA == null || componentB == null)
            {
                return;
            }

            switch (constraint.Type)
            {
                case ConstraintType.Coincident:
                case ConstraintType.Coaxial:
                case ConstraintType.Parallel:
                case ConstraintType.Perpendicular:
                case ConstraintType.Distance:
                case ConstraintType.Angle:
                case ConstraintType.Slider:
                    throw new NotImplementedException(
                        $"Constraint type '{constraint.Type}' is not yet resolved by the solver.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(constraint));
            }
        }
    }
}
