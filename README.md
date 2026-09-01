# CAD-Contrians-DIAPP — CAD Assembly Simulator

Herramienta interactiva para importar ensambles CAD (STEP/STP), definir constraints simples
entre componentes, simular secuencias de movimiento (robots, cilindros, mecanismos) y
controlarlas desde un panel custom por proyecto.

## Stack
- **Backend:** C# / .NET 8, con integración pendiente a Open CASCADE para el parseo de STEP.
- **Frontend:** WPF (viewport 3D nativo `Viewport3D` como punto de partida).
- **Serialización:** Newtonsoft.Json para secuencias y proyectos.

## Estructura

```
CADSimulator.sln
src/CADSimulator/
├── Core/               # Lógica de simulación
│   ├── AssemblyLoader.cs
│   ├── ConstraintSolver.cs
│   ├── ControlPanelLoader.cs
│   ├── KinematicSimulator.cs
│   └── SequenceRecorder.cs
├── UI/                  # Interfaz WPF
│   ├── MainWindow.xaml(.cs)
│   ├── SequencePanel.xaml(.cs)
│   ├── PropertyPanel.xaml(.cs)
│   └── ControlPad.xaml(.cs)
├── Models/              # Estructuras de datos
│   ├── Assembly.cs / Component.cs / Constraint.cs
│   ├── Sequence.cs (Frame, SequenceEvent) / Joint.cs
│   ├── Pose.cs / Vector3d.cs
│   └── ControlPanelDefinition.cs
└── Utils/
    ├── STEPParser.cs
    ├── MathHelper.cs
    └── FileManager.cs
Projects/               # Proyectos/presets guardados (ejemplos incluidos)
```

## Estado actual

Este es el scaffold inicial del proyecto:

- Modelos de datos (`Assembly`, `Component`, `Constraint`, `Joint`, `Sequence`) ya definidos.
- `KinematicSimulator` interpola poses entre keyframes (funcional).
- `SequenceRecorder` + `FileManager` graban y guardan/cargan secuencias en JSON (funcional).
- `ControlPanelLoader` parsea el XML de `<ControlPanel>` y `ControlPad` renderiza los controles
  dinámicamente (funcional).
- `ConstraintSolver` y `STEPParser` son stubs: la resolución geométrica de constraints y el
  parseo real de STEP requieren una integración con Open CASCADE (p. ej. `OccSharp`) que aún
  no está conectada.

## Próximos pasos sugeridos

1. Integrar un binding de Open CASCADE en `STEPParser` para poblar `Assembly`/`Component` desde
   un archivo `.step`/`.stp` real, y renderizar la geometría en el `Viewport3D`.
2. Implementar la resolución geométrica en `ConstraintSolver` por tipo de constraint
   (Coincident, Coaxial, Parallel, Perpendicular, Distance, Angle, Slider).
3. Reemplazar `Viewport3D` (WPF nativo) por un renderer más capaz si se necesita mejor
   performance o materiales/PBR (p. ej. Veldrid/DirectX) — opcional, no bloqueante.

> Nota: WPF solo compila/corre en Windows. Este scaffold fue escrito sin acceso a un SDK de
> .NET ni a Windows en este entorno, así que no se pudo compilar/ejecutar aquí — verificar con
> `dotnet build` en Windows antes de dar por buena la integración de cada pieza.
