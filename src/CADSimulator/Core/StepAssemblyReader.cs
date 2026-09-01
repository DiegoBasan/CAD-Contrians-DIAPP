using System.Collections.Generic;
using System.IO;
using System.Linq;
using CADSimulator.Models;
using CADSimulator.Utils;
using CADSimulator.Utils.Step;

namespace CADSimulator.Core
{
    /// <summary>
    /// Builds an Assembly (component hierarchy with exact per-instance placements, plus analytic
    /// face geometry for constraint targets) directly from a STEP/STP file's own product
    /// structure — no meshing or tessellation involved anywhere in this path. Targets the common
    /// AP214 assembly pattern used by mainstream CAD exporters (SolidWorks, Inventor, Fusion 360,
    /// Onshape, ...):
    ///   PRODUCT -&gt; PRODUCT_DEFINITION_FORMATION -&gt; PRODUCT_DEFINITION
    ///   NEXT_ASSEMBLY_USAGE_OCCURRENCE ties a parent PRODUCT_DEFINITION to a child one.
    ///   CONTEXT_DEPENDENT_SHAPE_REPRESENTATION -&gt; REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION
    ///   -&gt; ITEM_DEFINED_TRANSFORMATION gives the child's exact relative placement.
    ///   PRODUCT_DEFINITION_SHAPE -&gt; SHAPE_DEFINITION_REPRESENTATION -&gt; SHAPE_REPRESENTATION -&gt;
    ///   MANIFOLD_SOLID_BREP -&gt; CLOSED_SHELL -&gt; ADVANCED_FACE gives each part's exact BREP faces.
    /// Unusual/AP203 files that don't follow this pattern degrade gracefully: components fall
    /// back to an identity pose and/or no extracted faces rather than throwing.
    /// </summary>
    public static class StepAssemblyReader
    {
        public static Assembly Read(string filePath)
        {
            var text = File.ReadAllText(filePath);
            var raw = StepRawParser.Parse(text);
            return new StepAssemblyDocument(raw).BuildAssembly();
        }
    }

    internal class StepAssemblyDocument
    {
        private static readonly HashSet<string> ShapeRepresentationKeywords = new HashSet<string>
        {
            "SHAPE_REPRESENTATION",
            "ADVANCED_BREP_SHAPE_REPRESENTATION",
            "MANIFOLD_SURFACE_SHAPE_REPRESENTATION",
            "FACETED_BREP_SHAPE_REPRESENTATION",
            "GEOMETRICALLY_BOUNDED_SURFACE_SHAPE_REPRESENTATION"
        };

        private readonly StepRawParser _raw;
        private readonly Dictionary<int, StepEntity> _cdsrByNauoId;

        public StepAssemblyDocument(StepRawParser raw)
        {
            _raw = raw;
            _cdsrByNauoId = BuildCdsrByNauoLookup();
        }

        private StepEntity? Get(int id) => _raw.EntitiesById.TryGetValue(id, out var e) ? e : null;

        public Assembly BuildAssembly()
        {
            var assembly = new Assembly { Name = "Assembly" };

            var productDefinitions = _raw.EntitiesById.Values.Where(e => e.Is("PRODUCT_DEFINITION")).ToList();
            var nauos = _raw.EntitiesById.Values.Where(e => e.Is("NEXT_ASSEMBLY_USAGE_OCCURRENCE")).ToList();

            var relatedIds = new HashSet<int>(nauos
                .Where(n => n.Parameters.Count > 4)
                .Select(n => n.Parameters[4].AsReference()));

            var roots = productDefinitions.Where(pd => !relatedIds.Contains(pd.Id)).ToList();

            foreach (var root in roots)
            {
                assembly.Components.Add(BuildComponent(root, nauos, new Pose(), null, new HashSet<int>()));
            }

            if (assembly.Components.Count == 1)
            {
                assembly.Name = assembly.Components[0].Name;
            }

            return assembly;
        }

        /// <summary>
        /// Builds one component instance. `occurrenceNauoId` identifies *this specific placement*
        /// (the NAUO that introduced it) so that a part reused many times in the assembly (e.g. a
        /// bolt used 8 times) still gets a distinct Component.Id per occurrence, not one shared by
        /// the underlying product definition. `ancestorPdIds` guards against a malformed/cyclic
        /// NAUO graph recursing forever.
        /// </summary>
        private Component BuildComponent(StepEntity productDefinition, List<StepEntity> allNauos, Pose localPose, int? occurrenceNauoId, HashSet<int> ancestorPdIds)
        {
            var component = new Component
            {
                Id = occurrenceNauoId.HasValue ? "nauo-" + occurrenceNauoId.Value : "pd-" + productDefinition.Id,
                Name = GetProductName(productDefinition) ?? $"Component {productDefinition.Id}",
                Pose = localPose,
                Faces = ExtractFaces(productDefinition)
            };

            if (!ancestorPdIds.Add(productDefinition.Id))
            {
                return component; // Cycle detected — stop descending, keep this node as a leaf.
            }

            var childNauos = allNauos.Where(n => n.Parameters.Count > 3 && n.Parameters[3].AsReference() == productDefinition.Id);

            foreach (var nauo in childNauos)
            {
                var childPd = Get(nauo.Parameters[4].AsReference());
                if (childPd == null)
                {
                    continue;
                }

                var childPose = ResolveRelativePose(nauo.Id);
                component.Children.Add(BuildComponent(childPd, allNauos, childPose, nauo.Id, ancestorPdIds));
            }

            ancestorPdIds.Remove(productDefinition.Id);
            return component;
        }

        private string? GetProductName(StepEntity productDefinition)
        {
            if (productDefinition.Parameters.Count < 3)
            {
                return null;
            }

            var formation = Get(productDefinition.Parameters[2].AsReference());
            if (formation == null || formation.Parameters.Count < 3)
            {
                return null;
            }

            var product = Get(formation.Parameters[2].AsReference());
            if (product == null || product.Parameters.Count < 2)
            {
                return null;
            }

            return product.Parameters[1].AsText();
        }

        private Dictionary<int, StepEntity> BuildCdsrByNauoLookup()
        {
            var result = new Dictionary<int, StepEntity>();

            foreach (var pds in _raw.EntitiesById.Values.Where(e => e.Is("PRODUCT_DEFINITION_SHAPE")))
            {
                if (pds.Parameters.Count < 3)
                {
                    continue;
                }

                var nauoId = pds.Parameters[2].AsReference();
                var nauo = Get(nauoId);
                if (nauo == null || !nauo.Is("NEXT_ASSEMBLY_USAGE_OCCURRENCE"))
                {
                    continue;
                }

                foreach (var cdsr in _raw.EntitiesById.Values.Where(e => e.Is("CONTEXT_DEPENDENT_SHAPE_REPRESENTATION")))
                {
                    if (cdsr.Parameters.Count > 1 && cdsr.Parameters[1].AsReference() == pds.Id)
                    {
                        result[nauoId] = cdsr;
                    }
                }
            }

            return result;
        }

        /// <summary>Resolves the exact relative pose of a NAUO's child component, from its ITEM_DEFINED_TRANSFORMATION.</summary>
        private Pose ResolveRelativePose(int nauoId)
        {
            if (!_cdsrByNauoId.TryGetValue(nauoId, out var cdsr) || cdsr.Parameters.Count == 0)
            {
                return new Pose();
            }

            var relationship = Get(cdsr.Parameters[0].AsReference());
            var transformBlock = relationship?.GetBlock("REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION");
            if (transformBlock == null || transformBlock.Parameters.Count == 0)
            {
                return new Pose();
            }

            var itemDefinedTransform = Get(transformBlock.Parameters[0].AsReference());
            if (itemDefinedTransform == null || itemDefinedTransform.Parameters.Count < 4)
            {
                return new Pose();
            }

            var childAnchor = ReadAxis2Placement3D(itemDefinedTransform.Parameters[2].AsReference());
            var parentAnchor = ReadAxis2Placement3D(itemDefinedTransform.Parameters[3].AsReference());

            var relative = Frame3.Identity.RelativeTo(childAnchor).ComposeWithParent(parentAnchor);
            return relative.ToPose();
        }

        private Frame3 ReadAxis2Placement3D(int id)
        {
            var entity = Get(id);
            if (entity == null || entity.Parameters.Count < 2)
            {
                return Frame3.Identity;
            }

            var parameters = entity.Parameters;
            var origin = ReadCartesianPoint(parameters[1].AsReference());

            var z = parameters.Count > 2 && parameters[2].Kind == StepValueKind.Reference
                ? ReadDirection(parameters[2].AsReference())
                : new Vec3(0, 0, 1);

            var xHint = parameters.Count > 3 && parameters[3].Kind == StepValueKind.Reference
                ? ReadDirection(parameters[3].AsReference())
                : ArbitraryPerpendicular(z);

            var xProjected = xHint - (z * xHint.Dot(z));
            var x = xProjected.Length > 1e-9 ? xProjected.Normalized() : ArbitraryPerpendicular(z);
            var y = z.Cross(x).Normalized();

            return new Frame3 { Origin = origin, XAxis = x, YAxis = y, ZAxis = z };
        }

        private static Vec3 ArbitraryPerpendicular(Vec3 z)
        {
            var reference = System.Math.Abs(z.X) < 0.9 ? new Vec3(1, 0, 0) : new Vec3(0, 1, 0);
            return (reference - (z * reference.Dot(z))).Normalized();
        }

        private Vec3 ReadCartesianPoint(int id)
        {
            var entity = Get(id);
            if (entity == null || entity.Parameters.Count < 2)
            {
                return new Vec3(0, 0, 0);
            }

            var coords = entity.Parameters[1].AsList();
            return new Vec3(
                coords.Count > 0 ? coords[0].AsNumber() : 0,
                coords.Count > 1 ? coords[1].AsNumber() : 0,
                coords.Count > 2 ? coords[2].AsNumber() : 0);
        }

        private Vec3 ReadDirection(int id)
        {
            var entity = Get(id);
            if (entity == null || entity.Parameters.Count < 2)
            {
                return new Vec3(0, 0, 1);
            }

            var coords = entity.Parameters[1].AsList();
            var vector = new Vec3(
                coords.Count > 0 ? coords[0].AsNumber() : 0,
                coords.Count > 1 ? coords[1].AsNumber() : 0,
                coords.Count > 2 ? coords[2].AsNumber() : 0);
            return vector.Normalized();
        }

        private List<FaceGeometry> ExtractFaces(StepEntity productDefinition)
        {
            var faces = new List<FaceGeometry>();

            var shapeRepId = FindShapeRepresentationId(productDefinition.Id);
            if (shapeRepId == null)
            {
                return faces;
            }

            var shapeRep = Get(shapeRepId.Value);
            var shapeRepBlock = shapeRep?.Blocks.FirstOrDefault(b => ShapeRepresentationKeywords.Contains(b.Keyword));
            if (shapeRepBlock == null || shapeRepBlock.Parameters.Count < 2)
            {
                return faces;
            }

            CollectFacesFromItems(shapeRepBlock.Parameters[1].AsList(), faces, Frame3.Identity, 0);
            return faces;
        }

        /// <summary>
        /// Walks a SHAPE_REPRESENTATION's items. Most parts have their solid directly in this
        /// list (MANIFOLD_SOLID_BREP/BREP_WITH_VOIDS). Some CAD systems (SolidWorks in
        /// particular) instead share one master solid across every occurrence via MAPPED_ITEM ->
        /// REPRESENTATION_MAP, placing it into this representation through a second
        /// AXIS2_PLACEMENT_3D pair — `contextFrame` carries that (possibly nested) placement so
        /// extracted face geometry ends up in this component's own local coordinates either way.
        /// </summary>
        private void CollectFacesFromItems(List<StepValue> items, List<FaceGeometry> faces, Frame3 contextFrame, int depth)
        {
            if (depth > 8)
            {
                return; // guards against a malformed/cyclic REPRESENTATION_MAP chain.
            }

            foreach (var item in items)
            {
                if (item.Kind != StepValueKind.Reference)
                {
                    continue;
                }

                var itemEntity = Get(item.Reference);
                if (itemEntity == null)
                {
                    continue;
                }

                if (itemEntity.Is("MANIFOLD_SOLID_BREP") || itemEntity.Is("BREP_WITH_VOIDS"))
                {
                    CollectFacesFromSolid(itemEntity, faces, contextFrame);
                }
                else if (itemEntity.Is("MAPPED_ITEM"))
                {
                    CollectFacesFromMappedItem(itemEntity, faces, contextFrame, depth);
                }
            }
        }

        private void CollectFacesFromMappedItem(StepEntity mappedItem, List<FaceGeometry> faces, Frame3 contextFrame, int depth)
        {
            if (mappedItem.Parameters.Count < 3)
            {
                return;
            }

            var representationMap = Get(mappedItem.Parameters[1].AsReference());
            if (representationMap == null || representationMap.Parameters.Count < 2)
            {
                return;
            }

            var mappingOrigin = ReadAxis2Placement3D(representationMap.Parameters[0].AsReference());
            var mappingTarget = ReadAxis2Placement3D(mappedItem.Parameters[2].AsReference());

            // A point in the mapped representation's local coordinates first gets re-expressed
            // relative to mappingOrigin, then placed as if it were given relative to
            // mappingTarget — which lives in *this* representation's space.
            var mappingFrame = Frame3.Identity.RelativeTo(mappingOrigin).ComposeWithParent(mappingTarget);
            var nestedContext = mappingFrame.ComposeWithParent(contextFrame);

            var mappedRep = Get(representationMap.Parameters[1].AsReference());
            var mappedBlock = mappedRep?.Blocks.FirstOrDefault(b => ShapeRepresentationKeywords.Contains(b.Keyword));
            if (mappedBlock == null || mappedBlock.Parameters.Count < 2)
            {
                return;
            }

            CollectFacesFromItems(mappedBlock.Parameters[1].AsList(), faces, nestedContext, depth + 1);
        }

        private int? FindShapeRepresentationId(int productDefinitionId)
        {
            foreach (var pds in _raw.EntitiesById.Values.Where(e => e.Is("PRODUCT_DEFINITION_SHAPE")))
            {
                if (pds.Parameters.Count < 3 || pds.Parameters[2].AsReference() != productDefinitionId)
                {
                    continue;
                }

                foreach (var sdr in _raw.EntitiesById.Values.Where(e => e.Is("SHAPE_DEFINITION_REPRESENTATION")))
                {
                    if (sdr.Parameters.Count >= 2 && sdr.Parameters[0].AsReference() == pds.Id)
                    {
                        return sdr.Parameters[1].AsReference();
                    }
                }
            }

            return null;
        }

        private void CollectFacesFromSolid(StepEntity solid, List<FaceGeometry> faces, Frame3 contextFrame)
        {
            if (solid.Parameters.Count < 2)
            {
                return;
            }

            CollectFacesFromShell(Get(solid.Parameters[1].AsReference()), faces, contextFrame);

            if (solid.Is("BREP_WITH_VOIDS") && solid.Parameters.Count > 2)
            {
                foreach (var voidRef in solid.Parameters[2].AsList())
                {
                    if (voidRef.Kind == StepValueKind.Reference)
                    {
                        CollectFacesFromShell(Get(voidRef.Reference), faces, contextFrame);
                    }
                }
            }
        }

        private void CollectFacesFromShell(StepEntity? shell, List<FaceGeometry> faces, Frame3 contextFrame)
        {
            if (shell == null || shell.Parameters.Count < 2)
            {
                return;
            }

            foreach (var faceRef in shell.Parameters[1].AsList())
            {
                if (faceRef.Kind != StepValueKind.Reference)
                {
                    continue;
                }

                var faceGeometry = ExtractFaceGeometry(Get(faceRef.Reference), contextFrame);
                if (faceGeometry != null)
                {
                    faces.Add(faceGeometry);
                }
            }
        }

        private FaceGeometry? ExtractFaceGeometry(StepEntity? faceEntity, Frame3 contextFrame)
        {
            if (faceEntity == null || !faceEntity.Is("ADVANCED_FACE") || faceEntity.Parameters.Count < 3)
            {
                return null;
            }

            var surface = Get(faceEntity.Parameters[2].AsReference());
            if (surface == null)
            {
                return null;
            }

            if (surface.Is("PLANE") && surface.Parameters.Count >= 2)
            {
                var frame = ReadAxis2Placement3D(surface.Parameters[1].AsReference()).ComposeWithParent(contextFrame);
                return new FaceGeometry
                {
                    Type = SurfaceType.Planar,
                    Origin = new Vector3d(frame.Origin.X, frame.Origin.Y, frame.Origin.Z),
                    Axis = new Vector3d(frame.ZAxis.X, frame.ZAxis.Y, frame.ZAxis.Z),
                    BoundaryLoop = ExtractPlanarBoundaryLoop(faceEntity, contextFrame)
                };
            }

            if (surface.Is("CYLINDRICAL_SURFACE") && surface.Parameters.Count >= 3)
            {
                var frame = ReadAxis2Placement3D(surface.Parameters[1].AsReference()).ComposeWithParent(contextFrame);
                return new FaceGeometry
                {
                    Type = SurfaceType.Cylindrical,
                    Origin = new Vector3d(frame.Origin.X, frame.Origin.Y, frame.Origin.Z),
                    Axis = new Vector3d(frame.ZAxis.X, frame.ZAxis.Y, frame.ZAxis.Z),
                    Radius = surface.Parameters[2].AsNumber()
                };
            }

            return null;
        }

        /// <summary>
        /// Reads a planar ADVANCED_FACE's outer boundary as an ordered vertex loop, but only when
        /// it has a single bound (no holes) and every edge is a straight STEP LINE — otherwise
        /// returns empty rather than approximating a curved boundary as straight.
        /// </summary>
        private List<Vector3d> ExtractPlanarBoundaryLoop(StepEntity faceEntity, Frame3 contextFrame)
        {
            var empty = new List<Vector3d>();
            if (faceEntity.Parameters.Count < 2)
            {
                return empty;
            }

            var bounds = faceEntity.Parameters[1].AsList();
            if (bounds.Count != 1 || bounds[0].Kind != StepValueKind.Reference)
            {
                return empty;
            }

            var boundEntity = Get(bounds[0].Reference);
            if (boundEntity == null || boundEntity.Parameters.Count < 2)
            {
                return empty;
            }

            var loopEntity = Get(boundEntity.Parameters[1].AsReference());
            if (loopEntity == null || !loopEntity.Is("EDGE_LOOP") || loopEntity.Parameters.Count < 2)
            {
                return empty;
            }

            var loopPoints = new List<Vector3d>();
            foreach (var edgeRef in loopEntity.Parameters[1].AsList())
            {
                if (edgeRef.Kind != StepValueKind.Reference)
                {
                    return empty;
                }

                var orientedEdge = Get(edgeRef.Reference);
                if (orientedEdge == null || !orientedEdge.Is("ORIENTED_EDGE") || orientedEdge.Parameters.Count < 5)
                {
                    return empty;
                }

                var edgeCurve = Get(orientedEdge.Parameters[3].AsReference());
                if (edgeCurve == null || edgeCurve.Parameters.Count < 4)
                {
                    return empty;
                }

                var edgeGeometry = Get(edgeCurve.Parameters[3].AsReference());
                if (edgeGeometry == null || !edgeGeometry.Is("LINE"))
                {
                    return empty; // a curved edge — this face isn't a straight polygon.
                }

                var orientation = orientedEdge.Parameters[4].Kind == StepValueKind.Enumeration
                    && orientedEdge.Parameters[4].Text == "T";
                var startVertexRef = orientation ? edgeCurve.Parameters[1] : edgeCurve.Parameters[2];

                var vertex = Get(startVertexRef.AsReference());
                if (vertex == null || vertex.Parameters.Count < 2)
                {
                    return empty;
                }

                var point = contextFrame.TransformPoint(ReadCartesianPoint(vertex.Parameters[1].AsReference()));
                loopPoints.Add(new Vector3d(point.X, point.Y, point.Z));
            }

            return loopPoints;
        }
    }
}
