# Radiant Terrain Generator

## Features

* **Dual-Interface Design:** A WPF **GUI** for real-time 3D previews and a headless **CLI** for batch processing and pipeline integration.
* **Asset Management & Texture Browser:** Directly mounts and reads from `.pk3` archives and physical directories. [cite_start]Includes a custom interface with hierarchical grouping, a "Favorites" system [cite: 9][cite_start], and options to set a default texture or view raw shader information[cite: 10].
* **Persistent Configuration:** A dedicated configuration window lets you set and save your Game Data Path [cite: 55, 56][cite_start], Default Output Folder [cite: 56][cite_start], and Default Target Mode [cite: 57, 58] so you don't have to re-enter them every session.
* **CSG Math Library:** Automatically derives 3D bounding boxes by calculating the intersection of infinite planes—perfect for accurate editor placement.
* **Procedural Landforms & Interiors:** Built-in macro shapes including Hill, Crater, Ridge, Slope, Volcano, Valley, Tunnel, and Slope Tunnel[cite: 34, 35, 36]. The interior generators calculate fully enclosed, seamless cave meshes with matching floor, ceiling, and wall heightmaps.

* **Noise Layers:** Overlay Perlin, Simplex, or Random noise [cite: 40, 41] with adjustable frequency and variance.
* **Strict Grid Alignment:** Automatically snaps terrain bounds to fit sub-square sizes, preventing micro-leaks and ensuring perfect integer alignment in Radiant.
* **Advanced Modifiers:** Includes a **Terrace Step** feature for creating tiered, Minecraft-style, or "staircase" terrain[cite: 39].

---

## Getting Started

### Prerequisites
* [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or higher.
* Windows 10/11 (for the WPF GUI).

### Installation & Build
1.  **Clone the repository:**
    ```bash
    git clone [https://github.com/YourUsername/RadiantTerrainGenerator.git](https://github.com/YourUsername/RadiantTerrainGenerator.git)
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

## Usage: Graphical User Interface (GUI)

The GUI is designed for iterative design and visual feedback.

1.  **Configure Your Environment:** Open `View -> Configuration` to set your Game Data Path (e.g., your `etmain` folder) [cite: 55, 56] [cite_start]and Output Folder[cite: 56]. This allows the Texture Browser to properly mount your `.pk3` files.
2.  **Select Target Mode:** * **Use .map Hint Brush:** Browse for an existing `.map` file[cite: 22]. The tool will look for a `func_group` containing a single brush with the `common/hint` texture.
    **Manual Size:** Enter specific Width, Length, and Height[cite: 22]. The terrain will be centered at `(0,0,0)`.
3.  **Choose a Base Shape:** Selecting a shape like "Hill" or "Tunnel" will dynamically reveal specific height parameters [cite: 36, 37][cite_start], such as "Tunnel Height" for cave openings[cite: 38].
4.  **Pick a Texture:** Use the **Browse...** button to open the Texture Browser and select your top texture[cite: 32].
5.  **Adjust Resolution:** Select a **Sub-square Size** from the presets (powers of 2) [cite: 26, 27] [cite_start]or use **Advanced Mode** for custom X/Y resolutions[cite: 29].
6.  **Generate:** Click **GENERATE** to process the math and see the real-time 3D preview[cite: 46].
7.  **Export:** If the preview looks correct, click **EXPORT .MAP**[cite: 46, 47]. [cite_start]You can choose to override the existing map or save it as a new file[cite: 47].

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
