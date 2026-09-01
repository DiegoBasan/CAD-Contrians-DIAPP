# CAD-Contrians-DIAPP — CAD Assembly Simulator

Herramienta interactiva para importar ensambles CAD (STEP/STP), definir constraints simples
entre componentes, simular secuencias de movimiento (robots, cilindros, mecanismos) y
controlarlas desde un panel custom por proyecto.

**Importante:** esto trabaja sobre datos CAD exactos (BREP: sólidos con caras planas/cilíndricas/
NURBS analíticas, tal como vienen en el STEP), no sobre una malla triangulada tipo STL. El parser
lee la estructura de producto/ensamble y la geometría analítica directamente del archivo STEP.

## Stack
- **Backend:** C# / .NET 8. El parseo de STEP es un lector propio del formato físico STEP
  (ISO 10303-21) — no depende de Open CASCADE ni de ninguna malla intermedia.
- **Frontend:** WPF (viewport 3D nativo `Viewport3D` como punto de partida).
- **Serialización:** Newtonsoft.Json para secuencias y proyectos.

## Estructura

```
CADSimulator.sln
src/CADSimulator/
├── Core/                    # Lógica de simulación
│   ├── AssemblyLoader.cs
│   ├── StepAssemblyReader.cs   # Lee ensamble+geometría BREP desde STEP (real)
│   ├── ConstraintSolver.cs     # Stub: resolución geométrica por tipo de constraint
│   ├── ControlPanelLoader.cs
│   ├── KinematicSimulator.cs
│   └── SequenceRecorder.cs
├── UI/                       # Interfaz WPF
│   ├── MainWindow.xaml(.cs)
│   ├── SequencePanel.xaml(.cs)
│   ├── PropertyPanel.xaml(.cs)
│   └── ControlPad.xaml(.cs)
├── Models/                   # Estructuras de datos
│   ├── Assembly.cs / Component.cs / Constraint.cs
│   ├── Sequence.cs (Frame, SequenceEvent) / Joint.cs
│   ├── Pose.cs / Vector3d.cs / FaceGeometry.cs
│   └── ControlPanelDefinition.cs
└── Utils/
    ├── Step/                    # Lector genérico del formato físico STEP (ISO 10303-21)
    │   ├── StepValue.cs / StepEntity.cs
    │   ├── StepTextScanner.cs
    │   └── StepRawParser.cs
    ├── Geometry3.cs             # Vec3 / Frame3 (matemática de transformaciones)
    ├── MathHelper.cs
    └── FileManager.cs
Projects/                    # Proyectos/presets guardados (ejemplos incluidos)
```

## Cómo lee un STEP (sin mallado)

`StepRawParser` (Utils/Step) tokeniza el archivo STEP físico (ISO 10303-21) en una tabla genérica
de entidades `id -> (keyword, parámetros)`, sin conocer ningún esquema EXPRESS en particular.

`StepAssemblyReader` (Core) interpreta esas entidades siguiendo el patrón AP214 estándar que usan
SolidWorks/Inventor/Fusion 360/Onshape al exportar:

- `PRODUCT` → `PRODUCT_DEFINITION_FORMATION` → `PRODUCT_DEFINITION`: nombre de cada pieza/sub-ensamble.
- `NEXT_ASSEMBLY_USAGE_OCCURRENCE`: relación padre/hijo entre `PRODUCT_DEFINITION` (una pieza
  repetida N veces en el ensamble genera N ocurrencias/`Component`, cada una con su propio Id).
- `CONTEXT_DEPENDENT_SHAPE_REPRESENTATION` → `REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION` →
  `ITEM_DEFINED_TRANSFORMATION` (dos `AXIS2_PLACEMENT_3D`): la pose relativa exacta de cada
  ocurrencia respecto a su padre — sin aproximar, es la transformación tal cual la escribió el
  CAD de origen.
- `PRODUCT_DEFINITION_SHAPE` → `SHAPE_DEFINITION_REPRESENTATION` → `SHAPE_REPRESENTATION` →
  `MANIFOLD_SOLID_BREP` → `CLOSED_SHELL` → `ADVANCED_FACE`: por cada cara plana (`PLANE`) o
  cilíndrica (`CYLINDRICAL_SURFACE`) del sólido se extrae su geometría analítica exacta
  (origen, normal/eje, radio) a `Component.Faces`, pensada como objetivo de constraints
  (Coincident, Coaxial, Parallel, ...). Caras NURBS/free-form no se extraen todavía.

Archivos AP203 o exportadores atípicos que no sigan este patrón degradan sin explotar: el
componente cae a pose identidad y/o sin caras extraídas, en vez de lanzar una excepción.

## Estado actual

- Modelos de datos (`Assembly`, `Component`, `Constraint`, `Joint`, `Sequence`, `FaceGeometry`)
  definidos.
- **`StepAssemblyReader` + `StepRawParser` son reales**: leen jerarquía de ensamble, poses
  relativas exactas y geometría analítica de caras (plano/cilindro) directamente del STEP, sin
  pasar por ninguna malla. `MainWindow` ya importa un `.step`/`.stp` real y puebla el árbol de
  componentes con esto.
- `KinematicSimulator` interpola poses entre keyframes (funcional).
- `SequenceRecorder` + `FileManager` graban y guardan/cargan secuencias en JSON (funcional).
- `ControlPanelLoader` parsea el XML de `<ControlPanel>` y `ControlPad` renderiza los controles
  dinámicamente (funcional).
- `ConstraintSolver` sigue siendo un stub: ya tiene la geometría exacta disponible
  (`Component.Faces`) pero falta implementar la resolución por tipo de constraint.
- El `Viewport3D` todavía no dibuja la geometría importada: falta un tesselador (triangulación)
  de las caras BREP para poder mostrarlas — ver "Próximos pasos".

## Próximos pasos sugeridos

1. Tesselar las caras planas (polígono con bordes rectos vía `LINE`) para poder dibujarlas en el
   `Viewport3D`; caras cilíndricas/NURBS necesitan evaluación de curvas/superficies, que es un
   paso más grande (o integrar un kernel como Open CASCADE solo para esa parte si hace falta
   precisión total de visualización, dejando el parser de estructura/constraints como está).
2. Implementar la resolución geométrica en `ConstraintSolver` por tipo de constraint
   (Coincident, Coaxial, Parallel, Perpendicular, Distance, Angle, Slider) usando
   `Component.Faces`.
3. Probar `StepAssemblyReader` contra archivos STEP reales de distintos exportadores (SolidWorks,
   Inventor, Fusion 360, FreeCAD) y ajustar los índices de atributos si algún exportador se desvía
   del patrón AP214 asumido.

## Requisitos de build

- WPF solo compila/corre en Windows.
- El proyecto apunta a `net10.0-windows`. Antes de tocar `<TargetFramework>` en el `.csproj`,
  correr `dotnet --list-sdks` y usar la versión que ya esté instalada en la máquina — pedir una
  versión distinta a la instalada rompe el build pidiendo un SDK que puede requerir permisos de
  administrador para instalar.
- Ya se compiló y se probó importando un `.step` real (ver histórico del repo) tras ajustar el
  `TargetFramework` a la versión de SDK disponible en esa máquina.
