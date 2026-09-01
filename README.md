# CAD-Contrians-DIAPP — CAD Assembly Simulator

**Versión: v1.4** — ver [Changelog](#changelog) abajo. Cada iteración sube este número.

Herramienta interactiva para importar ensambles CAD (STEP/STP), definir constraints simples
entre componentes, simular secuencias de movimiento (robots, cilindros, mecanismos) y
controlarlas desde un panel custom por proyecto.

**Importante:** esto trabaja sobre datos CAD exactos (BREP: sólidos con caras planas/cilíndricas/
NURBS analíticas, tal como vienen en el STEP), no sobre una malla triangulada tipo STL. El parser
lee la estructura de producto/ensamble y la geometría analítica directamente del archivo STEP.

## Stack

- **Backend:** C# / .NET (target actual: `net10.0-windows` — ver Requisitos de build). El parseo
  de STEP es un lector propio del formato físico STEP (ISO 10303-21) — no depende de Open CASCADE
  ni de ninguna malla intermedia.
- **UI:** desde v1.4, la ventana WPF solo hospeda un control **WebView2** a pantalla completa; toda
  la interfaz (árbol, propiedades, constraints, canvas 3D) es una página HTML/CSS/JS local. El
  canvas 3D usa **Three.js** (vendorizado, MIT) con `OrbitControls` (cámara) y `TransformControls`
  (gizmo de mover/rotar), como en Shapr3D/SolidWorks.
- **Puente C# ↔ JS:** `CoreWebView2.PostWebMessageAsJson` (C#→JS) y `window.chrome.webview.postMessage`
  + `WebMessageReceived` (JS→C#). El STEP se sigue parseando en C# (fuente de verdad); solo el
  resultado ya teselado se manda al navegador para dibujarlo.
- **Serialización:** Newtonsoft.Json para el puente C#/JS y para guardar proyectos.

## Estructura

```
CADSimulator.sln
src/CADSimulator/
├── Core/                       # Lógica de simulación
│   ├── AssemblyLoader.cs
│   ├── StepAssemblyReader.cs      # Lee ensamble+geometría BREP desde STEP (real)
│   ├── AssemblySceneExport.cs     # Assembly -> DTO JSON (tesela caras planas) para el frontend
│   ├── ConstraintSolver.cs        # Stub: resolución geométrica por tipo de constraint
│   ├── ControlPanelLoader.cs
│   ├── KinematicSimulator.cs
│   └── SequenceRecorder.cs
├── UI/
│   └── MainWindow.xaml(.cs)       # Host de WebView2 + puente de mensajes con la página HTML
├── Models/                      # Estructuras de datos
│   ├── Assembly.cs / Component.cs / Constraint.cs
│   ├── Sequence.cs (Frame, SequenceEvent) / Joint.cs
│   ├── Pose.cs / Vector3d.cs / FaceGeometry.cs
│   └── ControlPanelDefinition.cs
├── Utils/
│   ├── Step/                      # Lector genérico del formato físico STEP (ISO 10303-21)
│   │   ├── StepValue.cs / StepEntity.cs
│   │   ├── StepTextScanner.cs
│   │   └── StepRawParser.cs
│   ├── Geometry3.cs                # Vec3 / Frame3 (matemática de transformaciones)
│   ├── PolygonTessellator.cs       # Ear-clipping para caras planas (usado por AssemblySceneExport)
│   ├── MathHelper.cs
│   └── FileManager.cs
└── wwwroot/                      # Frontend (copiado al output de build)
    ├── index.html / app.css / app.js
    └── vendor/three.min.js, OrbitControls.js, TransformControls.js  (three@0.128.0, MIT)
Projects/                       # Proyectos/presets guardados (ejemplos incluidos)
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
- **Geometría compartida (`MAPPED_ITEM`/`REPRESENTATION_MAP`)**: desde v1.4 también se sigue este
  patrón — común en SolidWorks/robots con piezas repetidas — donde una representación reutiliza el
  sólido de otra a través de dos `AXIS2_PLACEMENT_3D` (origen en la representación compartida +
  destino en la que la usa). Si tu ensamble mostraba "(0 faces)" en todas las piezas en v1.3, esto
  debería arreglarlo.

Archivos AP203 o exportadores atípicos que no sigan ninguno de estos dos patrones degradan sin
explotar: el componente cae a pose identidad y/o sin caras extraídas, en vez de lanzar una excepción.

## La interfaz (WebView2 + Three.js)

Todo el frontend vive en `wwwroot/` y es HTML/CSS/JS plano (sin build step, sin npm — los archivos
de Three.js están vendorizados directamente en el repo):

- **Import STEP / Save Project** (toolbar): disparan un mensaje a C#, que abre el diálogo nativo,
  parsea el archivo con `StepAssemblyReader`, y devuelve el árbol ya teselado (`AssemblySceneExport`)
  como JSON.
- **Árbol de ensamble** (panel izquierdo): jerárquico, clic para seleccionar.
- **Canvas 3D** (centro): Three.js, cámara Z-up (convención CAD) con `OrbitControls` (izq=orbitar,
  der/medio=pan, rueda=zoom) que se ajusta automáticamente al tamaño del modelo importado. Clic
  sobre una pieza en el canvas también la selecciona (raycasting).
- **Mover / Rotar** (toolbar + gizmo): al seleccionar una pieza aparece un gizmo `TransformControls`
  sobre ella; los botones "Move"/"Rotate" cambian el modo del gizmo. Arrastrar el gizmo mueve/rota
  la pieza en vivo, tal como en Shapr3D/SolidWorks — ya no es edición por campos numéricos.
- **Constraints** (panel derecho): "Use selected as A/B" toma la pieza actualmente seleccionada,
  se elige el tipo, y "Add Constraint" la agrega a la lista en memoria (el `ConstraintSolver` en
  C# todavía no la resuelve geométricamente).
- **Save Project** manda el árbol (con las poses ya editadas por el usuario) + la lista de
  constraints a C#, que lo guarda tal cual como JSON en una carpeta `Projects/` junto al `.exe`.

## Estado actual

- **`StepAssemblyReader` + `StepRawParser` son reales**: leen jerarquía de ensamble, poses
  relativas exactas y geometría analítica de caras (plano/cilindro) directamente del STEP —
  incluyendo geometría compartida vía `MAPPED_ITEM` (nuevo en v1.4) — sin pasar por ninguna malla.
- **Viewport 3D real en Three.js**: tesela caras planas (`Component.Faces.BoundaryLoop`, bordes
  rectos) vía ear-clipping y las dibuja con jerarquía correcta (transform por componente).
- **Seleccionar + mover/rotar con gizmo** directamente en el canvas 3D (`TransformControls`), no
  solo campos numéricos.
- **Autoría de constraints** por selección en el canvas/árbol (todavía sin resolución geométrica).
- `KinematicSimulator` interpola poses entre keyframes (funcional, backend, aún no conectado a la
  UI web).
- `SequenceRecorder` + `FileManager` graban y guardan/cargan secuencias en JSON (funcional,
  backend).
- `ControlPanelLoader` parsea el XML de `<ControlPanel>` (funcional, backend; todavía no tiene UI
  en la página web — ver "Próximos pasos").
- `ConstraintSolver` sigue siendo un stub: ya tiene la geometría exacta disponible
  (`Component.Faces`) pero falta implementar la resolución por tipo de constraint.

## Limitaciones conocidas

- Solo se dibujan caras **planas** con contorno de **bordes rectos y sin agujeros** (un solo
  `FACE_BOUND`). Caras cilíndricas, NURBS, o con agujeros no se tesela aún — sí se sigue
  extrayendo su geometría analítica para constraints, pero no aparecen en el viewport. Es normal
  ver el modelo "incompleto" (solo caras planas) en piezas con mucho maquinado curvo.
- Si un ensamble sigue mostrando "(0 faces)" en todas las piezas después de v1.4, probablemente usa
  un patrón de STEP distinto a los dos ya soportados (AP214 directo y `MAPPED_ITEM`). Comparte
  unas líneas del `.step` con `SHAPE_REPRESENTATION`, `MANIFOLD_SOLID_BREP` o `MAPPED_ITEM` para
  ajustar el lector.
- La interacción click-vs-arrastrar-gizmo (selección por raycasting vs. `TransformControls`) es la
  parte más sensible a probar de verdad en Windows — no se pudo verificar en este entorno.

## Próximos pasos sugeridos

1. Conectar `ConstraintSolver` a la UI: que fijar una relación realmente reposicione las piezas
   dependientes (usando `Component.Faces` para Coincident/Coaxial/Parallel/...).
2. Caras con agujeros (múltiples `FACE_BOUND`) y caras cilíndricas/NURBS en el tesselador, para que
   el modelo se vea completo.
3. Mover el Control Panel (botones/sliders del proyecto, `ControlPanelDefinition`) y el timeline de
   secuencias (`SequenceRecorder`/`KinematicSimulator`) a la página web, igual que se hizo con el
   árbol y las propiedades.
4. Cargar/guardar el proyecto completo (no solo un JSON de una sola vez) desde la página, incluyendo
   reabrir un proyecto guardado.

## Requisitos de build / runtime

- WPF solo compila/corre en Windows.
- El proyecto apunta a `net10.0-windows`. Antes de tocar `<TargetFramework>` en el `.csproj`,
  correr `dotnet --list-sdks` y usar la versión que ya esté instalada en la máquina — pedir una
  versión distinta a la instalada rompe el build pidiendo un SDK que puede requerir permisos de
  administrador para instalar.
- **Nuevo en v1.4:** requiere el **WebView2 Runtime** en la máquina donde corre. Viene preinstalado
  de fábrica en Windows 11 y en la mayoría de instalaciones de Windows 10 actualizadas (es el motor
  del propio Edge), así que normalmente no hay que instalar nada — pero si la ventana abre en
  blanco o WebView2 tira un error al iniciar, es la señal de que falta, y el instalador
  ("Evergreen Bootstrapper") sí puede requerir permisos de administrador. Avísame si te pasa esto.
- Three.js/OrbitControls/TransformControls están vendorizados en `wwwroot/vendor/` (three@0.128.0,
  licencia MIT) — no hace falta internet en tiempo de ejecución ni npm/node para compilar.

## Changelog

- **v1.0** — Scaffold inicial: estructura de proyecto WPF, modelos de datos, `KinematicSimulator`,
  `SequenceRecorder`/`FileManager`, `ControlPanelLoader`/`ControlPad`. `STEPParser` y
  `ConstraintSolver` como stubs.
- **v1.1** — Lector real de STEP/BREP (sin mallado): `StepRawParser` + `StepAssemblyReader`
  reemplazan el stub, leyendo jerarquía de ensamble, poses exactas y geometría analítica de caras
  directamente del archivo STEP.
- **v1.2** — Fix de build: `TargetFramework` a `net10.0-windows` para coincidir con el SDK
  instalado (evita requerir permisos de admin para instalar otro SDK).
- **v1.3** — Viewport 3D nativo WPF (tesselado de caras planas + cámara orbital), modo oscuro,
  selección de partes en el árbol con edición de pose por campos numéricos, autoría de constraints
  por selección, y extracción del contorno de caras (`BoundaryLoop`) necesaria para poder
  dibujarlas.
- **v1.4** — Pivote de interfaz: toda la UI pasa a una página HTML/CSS/JS servida dentro de un
  WebView2, con Three.js para el canvas 3D (cámara `OrbitControls`, gizmo `TransformControls` para
  mover/rotar piezas arrastrando, como Shapr3D/SolidWorks). Se retira el `Viewport3D`/`PropertyPanel`
  /`ControlPad`/`SequencePanel` nativos de WPF (reemplazados por la página web). `StepAssemblyReader`
  ahora también soporta geometría compartida vía `MAPPED_ITEM`/`REPRESENTATION_MAP`, que era la
  causa más probable de ver "(0 faces)" en ensambles reales como el M-10iA probado.
