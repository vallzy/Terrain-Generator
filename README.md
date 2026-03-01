# Radiant Terrain Generator

## Features

* **Dual-Interface Design:** A WPF **GUI** for real-time 3D previews and a headless **CLI** for batch processing and pipeline integration.
* **Asset Management & Texture Browser:** Directly mounts and reads from `.pk3` archives and physical directories. Includes a custom interface with hierarchical grouping, a "Favorites" system, and options to set a default texture or view raw shader information.
* **Persistent Configuration:** A dedicated configuration window lets you set and save your Game Data Path, Default Output Folder, and Default Target Mode so you don't have to re-enter them every session.
* **CSG Math Library:** Automatically derives 3D bounding boxes by calculating the intersection of infinite planes—perfect for accurate editor placement.
* **Procedural Landforms & Interiors:** Built-in macro shapes including Hill, Crater, Ridge, Slope, Volcano, Valley, Tunnel, and Slope Tunnel. The interior generators calculate fully enclosed, seamless cave meshes with matching floor, ceiling, and wall heightmaps.
* **Noise Layers:** Overlay Perlin, Simplex, or Random noise with adjustable frequency and variance.
* **Strict Grid Alignment:** Automatically snaps terrain bounds to fit sub-square sizes, preventing micro-leaks and ensuring perfect integer alignment in Radiant.
* **Advanced Modifiers:** Includes a **Terrace Step** feature for creating tiered, Minecraft-style, or "staircase" terrain.

---

## Getting Started

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher.
* Windows 10/11 (for the WPF GUI).

### Installation & Build
1.  **Clone the repository:**
    ```bash
    git clone https://github.com/vallzy/Terrain-Generator.git
    ```
2.  **Build the project:**
    ```bash
    dotnet build -c Release
    ```
3.  **Publish a Single-File Executable:**
    To create a portable `.exe` with no dependencies, run:
    ```bash
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
    ```

---

## Quick Start Guide

The generator operates in two primary modes to define the 3D volume of your terrain: **Manual** and **Use .map Hint Brush**. 

**Using an existing .map file (Hint Brush Mode):**
If you choose to inject terrain directly into an existing `.map` file, **the program expects the `.map` to have a single hint brush tied to a `func_group` entity**. 
1. Open your map in Radiant.
2. Draw a brush that represents the exact bounding box (width, length, and max height) of your desired terrain.
3. Apply the `common/hint` texture to all faces of this brush.
4. Select the brush and turn it into a `func_group` (Right Click -> `func_group`). Ensure no other brushes are in this specific `func_group`.
5. Save your map and run it through the Terrain Generator. The program will automatically find this brush, calculate its 3D volume, and replace it with the generated procedural terrain.

**Using Manual Mode:**
If you just want to generate a standalone prefab block of terrain, select "Manual Size" and input your desired Width, Length, and Height. The terrain will be generated centered at world origin `(0,0,0)`.

---

## Usage: Graphical User Interface (GUI)

The GUI is designed for iterative design and visual feedback.

1.  **Configure Your Environment:** Open `View -> Configuration` to set your Game Data Path (e.g., your `etmain` folder) and Output Folder. This allows the Texture Browser to properly mount your `.pk3` files.
2.  **Select Target Mode:** Choose between targeting a Hint Brush in a `.map` or using Manual bounds.
3.  **Choose a Base Shape:** Selecting a shape like "Hill" or "Tunnel" will dynamically reveal specific height parameters, such as "Tunnel Height" for cave openings.
4.  **Pick a Texture:** Use the **Browse...** button to open the Texture Browser and select your top texture.
5.  **Adjust Resolution:** Select a **Sub-square Size** from the presets (powers of 2) or use **Advanced Mode** for custom X/Y resolutions.
6.  **Generate:** Click **GENERATE** to process the math and see the real-time 3D preview.
7.  **Export:** If the preview looks correct, click **EXPORT .MAP**. You can choose to override the existing map or save it as a new file.

---

## Usage: Command Line Interface (CLI)

The tool operates as a powerful headless utility when arguments are passed.

### Parameters
| Flag | Values | Description |
| :--- | :--- | :--- |
| `--mode` | `hint`, `manual` | Target mode (Default: `hint`) |
| `--file` | `Path` | Path to input `.map` file (Required for `hint`) |
| `--width / --length / --height` | `Number` | Dimensions for `manual` mode |
| `--subx / --suby` | `Power of 2` | Resolution of the terrain grid |
| `--shape` | `hill`, `crater`, `ridge`, `slope`, `volcano`, `valley`, `cave`, `slopetunnel` | The base macro shape |
| `--shapeheight` | `Number` | The height/depth of the macro shape landform |
| `--tunnelheight` | `Number` | Tunnel opening height (specifically for `slopetunnel`) |
| `--noise` | `perlin`, `simplex`, `random` | The procedural noise algorithm |
| `--variance` | `Number` | Maximum vertical noise offset |
| `--frequency` | `Number` | Scale of noise (e.g., `0.005`) |
| `--terrace` | `Number` | Z-height step increments (0 for smooth) |
| `--texture` | `Name` | The Radiant texture for the top faces |
| `--override` | `Flag` | Overwrites the input file (Hint mode only) |
| `--out` | `Name` | Custom filename for the output |

### CLI Examples

**Standalone Valley Generation:**
```powershell
./MapTerrainGenerator.exe --mode manual --width 4096 --length 4096 --shape valley --shapeheight 512 --out my_valley_map
