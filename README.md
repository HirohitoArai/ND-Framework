# A Unified Framework for 3D/4D Visualization and Simulation
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.17621258.svg)](https://doi.org/10.5281/zenodo.17621258)

This repository provides a Unity implementation of the software described in the accompanying paper. The current implementation and evaluation in this repository cover 3D and 4D.

## ✨ Features
The current release includes the following components (3D/4D).

<table border="0" align="center">
  <tr>
    <td align="center" width="33%">
      <img src="https://github.com/user-attachments/assets/c7dffe66-7a0e-4f9b-9d2e-03f1b6fe948d" alt="Parallel Slicing Demo">
    </td>
    <td align="center" width="33%">
      <img src="https://github.com/user-attachments/assets/ec7c236d-4340-4262-918e-45d4445b95de" alt="4D Rotation Demo">
    </td>
    <td align="center" width="33%">
      <img src="https://github.com/user-attachments/assets/82eb06b6-0874-470e-bf67-7415a5e56dcd" alt="XPBD Physics Demo">
    </td>
  </tr>
  <tr>
    <td align="center"><em>Parallel Slicing</em></td>
    <td align="center"><em>Rotation in the XW-plane</em></td>
    <td align="center"><em>XPBD physics simulation</em></td>
  </tr>
</table>

*   **Convex Hull Generation**: Generate convex meshes from point clouds (Quickhull-based implementation).
*   **Interactive 4D Visualization**: Explore 4D objects by viewing their 3D cross-sections through hyperplane slicing.
*   **Boolean Operations**: Perform Union, Intersection, and Difference operations on 3D/4D meshes interactively during Play Mode. Latency depends on mesh complexity.
*   **Topology/geometry separation**: The data model separates connectivity (topology) and embedding (geometry) to support additional systems (e.g., physics simulation).

## Environment Setup
- **Unity**: 2022.3.6f1 or later (LTS recommended).
- **OS tested**: Windows 10/11 (64-bit). Other OS versions are not validated in this repository.
- **GPU/Driver**: No special requirement beyond Unity’s system requirements.

Setup steps:
1. Clone this repository (or download the source as a ZIP).
2. Open **Unity Hub** → **Add** → select the repository folder.
3. Open the project with **Unity 2022.3.6f1+**.
4. Open a scene under `Assets/Scenes` and press **Play**.

Notes:
- Performance and responsiveness depend on mesh size and the selected operation.


## 🔧 Usage
### Creating a Convex Hull Mesh
You can generate a mesh from point cloud data and save it.

<img width="800" alt="CreateConvexHull" src="https://github.com/user-attachments/assets/83dc93ac-5a43-4cf3-a14e-2ad50493924c">

1.  From the top menu bar in the Unity Editor, click **[Tool]** → **[3DMesh/4DMesh]**.
2.  Choose the desired save format:
*   **Scriptable Object**: A Unity-native format that can be directly attached to a GameObject.
*   **JSON**: A human-readable format. Allows you to inspect the mesh data structure but cannot be used directly in the scene without conversion.

### Performing a Boolean Operation

You can perform Boolean operations on two meshes in Play Mode.

<img width="400" alt="ClickPlay" src="https://github.com/user-attachments/assets/b09c9001-f24e-4dd6-bd7a-12d10b5fe4c2">
<img width="500" alt="BooleanOperation" src="https://github.com/user-attachments/assets/44671ce9-6065-41a8-b502-b0aafec27604">

1.  Enter **Play Mode** in the Unity Editor.
2.  In the **Hierarchy** window, select the `BooleanManager3D` or `BooleanManager4D` GameObject.
3.  In the **Inspector** window, find the **Boolean Manager (Script)** component.
4.  Click the three dots (⋮) icon on the component and select the type of operation you want to execute (e.g., `Union`, `Intersection`).

<img height="250" alt="ConvertJSON" src="https://github.com/user-attachments/assets/ea5fc84b-0b7e-4c7b-9cb1-a7b567303c9f" />
<img height="250" alt="Convert" src="https://github.com/user-attachments/assets/10dac4c9-d0e8-48c1-9e4e-21b6bf2cac1a" />

5. JSON will be generated in the `Assets` folder, so right-click and select `Convert to 3D/4D Mesh(ScriptableObject)` to convert it to a scriptable object.

### Importing & Exporting `.plex` Files
#### Export
1. right-click the mesh-data(ScriptableObject).
2. click the `Export as plex file`.

#### Import
1. Right-click a blank area of the Project window.
2. click the `Import from plex file`.
3. Select the plex file.

#### Convert plex to JSON
1. Right-click a blank area of the Project window.
2. click the `Convert plex to JSON`.
3. Select the plex file.

## Component Reference
This section provides a detailed breakdown of the core components (C# scripts) used in this framework.

### RenderMesh
**Path:**
*   **3D:** `Assets/Scripts/3D/Mesh3D/RenderMesh3D.cs`
*   **4D:** `Assets/Scripts/4D/Mesh4D/RenderMesh4D.cs`

**Properties**
*   **Material**: The material to use to render the mesh. 
*   **Mesh Data**: Attach the ScriptableObject format mesh data.  
*   **Character Transform**: Attach the character object with camera.
*   **Slice W**: Define the offset of slicing hyperplane.

### Transform
**Path:**
*   **3D:** `Assets/Scripts/3D/World3D/Transform3D.cs`
*   **4D:** `Assets/Scripts/4D/World4D/Transform4D.cs`

**Properties**
*   **Position**: Controls the object's location in its local coordinate space. 
*   **Rotation**: Controls the object's orientation.  
*   **Scale**: Determines the size of the object along each local axis.

### Move
**Path:**
*   **3D:** `Assets/Scripts/3D/World3D/Move3D.cs`
*   **4D:** `Assets/Scripts/4D/World4D/Move4D.cs`

**Properties**
*   **Mouse Sensitivity**: Determines the rotational speed of the camera in response to mouse movement.
*   **Speed**: Sets the translation speed for the character or camera when using keyboard inputs.  
*   **Move**: **(Read-Only)** Displays the object's current movement vector in real-time.
*   **Forward**: **(Read-Only)** Displays the object's current forward-facing direction vector.
*   **Can**: Attach the camera here.

### PhysicsManager
**Path:**
*   **3D:** `Assets/Scripts/3D/physics3D/PhysicsManager3D.cs`
*   **4D:** `Assets/Scripts/4D/physics4D/PhysicsManager4D.cs`

**Properties**
*   **No**

**Description**
Attach this component to the only one empty object to enable physics.

### Collider
**Path:**
*   **3D:** `Assets/Scripts/3D/physics3D/Collider3D.cs`
*   **4D:** `Assets/Scripts/4D/physics4D/Collider4D.cs`

**Properties**
*   **No**

**Description**
Attach this component to the object you want to add collision detection to.

### PhysicsBody
**Path:**
*   **3D:** `Assets/Scripts/3D/physics3D/PhysicsBody3D.cs`
*   **4D:** `Assets/Scripts/4D/physics4D/PhysicsBody4D.cs`

**Properties**
*   **Mass**: Controls the object's total mass, influencing its inertia and reaction to forces.
*   **Sub Steps**: The number of physics simulation steps performed per FixedUpdate call. Higher values increase stability and accuracy at the cost of performance.
*   **Solver Iteration**: The number of times the constraint solver runs per sub-step. 
*   **Shape Matching Compliance**: The inverse of stiffness for the shape-matching constraint. A value of 0.0 is perfectly rigid; small positive values allow for soft-body deformation.
*   **Damping**: A factor that reduces the object's velocity over time, helping to stabilize the simulation and prevent excessive bouncing or jiggling.
*   **Gravity**: A vector representing the constant gravitational force.


### BooleanManager
**Path:**
*   **3D:** `Assets/Scripts/3D/Boolean3D/BooleanManager3D.cs`
*   **4D:** `Assets/Scripts/4D/Boolean4D/BooleanManager4D.cs`

**Properties**
*   **Object A**: The first mesh to be used in the operation. 
*   **Object B**: The second mesh to be used in the operation.  
*   **CELLSIZE**: Spatial division cell size.

## Citation

If you use this software in your research, please cite it as:

```bibtex
@software{hirohito_arai_2026_18247896,
  author       = {Hirohito Arai},
  title        = {HirohitoArai/unified-3d-4d-framework: 1.0.1},
  month        = jan,
  year         = 2026,
  publisher    = {Zenodo},
  version      = {1.0.1},
  doi          = {10.5281/zenodo.18247896},
  url          = {https://doi.org/10.5281/zenodo.18247896},
}
```

## License
This project is licensed under the MIT License.

## Author
*   **Hirohito Arai** (gpepper.works@gmail.com)
