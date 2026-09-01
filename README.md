# CAD-Contrians-DIAPP — CAD Assembly Simulator

**Versión: v1.3** — ver [Changelog](#changelog) abajo. Cada iteración sube este número.

Herramienta interactiva para importar ensambles CAD (STEP/STP), definir constraints simples
entre componentes, simular secuencias de movimiento (robots, cilindros, mecanismos) y
controlarlas desde un panel custom por proyecto.

**Importante:** esto trabaja sobre datos CAD exactos (BREP: sólidos con caras planas/cilíndricas/
NURBS analíticas, tal como vienen en el STEP), no sobre una malla triangulada tipo STL. El parser
lee la estructura de producto/ensamble y la geometría analítica directamente del archivo STEP.

## Stack
- **Backend:** C# / .NET (target actual: `net10.0-windows` — ver Requisitos de build). El parseo
  de STEP es un lector propio del formato físico STEP
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
  pasar por ninguna malla.
- **Viewport 3D real**: `AssemblyViewportBuilder` tesela las caras planas (`Component.Faces` con
  `BoundaryLoop` de bordes rectos) vía ear-clipping (`Utils/PolygonTessellator.cs`) y arma el
  `Model3DGroup` jerárquico (una transformación por componente, igual que la jerarquía del
  ensamble). `MainWindow` importa el `.step`, puebla el árbol, dibuja la escena y ajusta la
  cámara al bounding box del modelo. Cámara orbital con mouse: click-izq arrastra = orbitar,
  click-der/medio arrastra = pan, rueda = zoom.
- **Selección + mover partes**: seleccionar un componente en el árbol lo carga en `PropertyPanel`
  (Position/Rotation en cajas de texto); "Apply Pose" actualiza `Component.Pose` y refresca el
  viewport. Todavía es edición numérica, no arrastrar en 3D (ver "Próximos pasos").
- **Autoría de constraints**: en `PropertyPanel`, "Use selected as A/B" toma el componente
  seleccionado en el árbol, se elige el tipo, y "Add Constraint" agrega un `Constraint` a
  `assembly.Constraints` (todavía no se resuelve geométricamente — ver `ConstraintSolver`).
- Modo oscuro aplicado a toda la app vía estilos implícitos en `App.xaml`.
- `KinematicSimulator` interpola poses entre keyframes (funcional).
- `SequenceRecorder` + `FileManager` graban y guardan/cargan secuencias en JSON (funcional).
- `ControlPanelLoader` parsea el XML de `<ControlPanel>` y `ControlPad` renderiza los controles
  dinámicamente (funcional).
- `ConstraintSolver` sigue siendo un stub: ya tiene la geometría exacta disponible
  (`Component.Faces`) pero falta implementar la resolución por tipo de constraint.

## Limitaciones conocidas (viewport)

- Solo se dibujan caras **planas** con contorno de **bordes rectos y sin agujeros** (un solo
  `FACE_BOUND`). Caras cilíndricas, NURBS, o con agujeros no se tesela aún — sí se sigue
  extrayendo su geometría analítica para constraints, pero no aparecen en el viewport. Esto
  significa que, dependiendo de la pieza, es normal ver el modelo "incompleto" (solo caras
  planas) o vacío si la pieza es puramente curva.
- Si el árbol muestra "(0 faces)" en todos los componentes de un ensamble real, probablemente el
  archivo usa un patrón de STEP distinto al asumido (p. ej. geometría compartida vía
  `MAPPED_ITEM`/`REPRESENTATION_MAP` en vez de un `MANIFOLD_SOLID_BREP` directo). Si te pasa,
  compárteme unas líneas del `.step` con `MANIFOLD_SOLID_BREP`, `SHAPE_REPRESENTATION` o
  `MAPPED_ITEM` para ajustar el lector a ese exportador.

## Próximos pasos sugeridos

1. Mover partes arrastrando en el viewport (gizmo de traslación/rotación), no solo por campos
   numéricos.
2. Soportar `MAPPED_ITEM`/`REPRESENTATION_MAP` (geometría compartida entre instancias) en
   `StepAssemblyReader`, y caras con agujeros (múltiples `FACE_BOUND`) en el tesselador.
3. Implementar la resolución geométrica en `ConstraintSolver` por tipo de constraint
   (Coincident, Coaxial, Parallel, Perpendicular, Distance, Angle, Slider) usando
   `Component.Faces`, y que mover/restringir una parte mueva las que dependen de ella.
4. Tesselar caras cilíndricas/NURBS (evaluación de curvas/superficies) para que el modelo se vea
   completo, no solo sus caras planas.

## Requisitos de build

- WPF solo compila/corre en Windows.
- El proyecto apunta a `net10.0-windows`. Antes de tocar `<TargetFramework>` en el `.csproj`,
  correr `dotnet --list-sdks` y usar la versión que ya esté instalada en la máquina — pedir una
  versión distinta a la instalada rompe el build pidiendo un SDK que puede requerir permisos de
  administrador para instalar.
- Ya se compiló y se probó importando un `.step` real (ver histórico del repo) tras ajustar el
  `TargetFramework` a la versión de SDK disponible en esa máquina.

## Changelog

- **v1.0** — Scaffold inicial: estructura de proyecto WPF, modelos de datos, `KinematicSimulator`,
  `SequenceRecorder`/`FileManager`, `ControlPanelLoader`/`ControlPad`. `STEPParser` y
  `ConstraintSolver` como stubs.
- **v1.1** — Lector real de STEP/BREP (sin mallado): `StepRawParser` + `StepAssemblyReader`
  reemplazan el stub, leyendo jerarquía de ensamble, poses exactas y geometría analítica de caras
  directamente del archivo STEP.
- **v1.2** — Fix de build: `TargetFramework` a `net10.0-windows` para coincidir con el SDK
  instalado (evita requerir permisos de admin para instalar otro SDK).
- **v1.3** — Viewport 3D funcional (tesselado de caras planas + cámara orbital), modo oscuro,
  selección de partes en el árbol con edición de pose (mover partes), autoría de constraints por
  selección en `PropertyPanel`, y extracción del contorno de caras (`BoundaryLoop`) necesaria
  para poder dibujarlas.
