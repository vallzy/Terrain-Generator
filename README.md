# Terrain Generator

## Features

* **Dual-Interface Design:** A WPF **GUI** for real-time 3D previews and a headless **CLI** for batch processing and pipeline integration.
* **CSG Math Library:** Automatically derives 3D bounding boxes by calculating the intersection of infinite planes—perfect for accurate editor placement.
* **Procedural Landforms:** Built-in macro shapes including Hill, Crater, Ridge, Slope, Volcano, and Valley.
* **Noise Layers:** Overlay Perlin, Simplex, or Random noise with adjustable frequency and variance.
* **Strict Grid Alignment:** Automatically snaps terrain bounds to fit sub-square sizes, preventing micro-leaks and ensuring perfect integer alignment in Radiant.
* **Advanced Modifiers:** Includes a **Terrace Step** feature for creating tiered, Minecraft-style, or "staircase" terrain.

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

1.  **Select Target Mode:** * **Use .map Hint Brush:** Browse for an existing `.map` file. The tool will look for a `func_group` containing a single brush with the `common/hint` texture.
    * **Manual Size:** Enter specific Width, Length, and Height. The terrain will be centered at `(0,0,0)`.
2.  **Choose a Base Shape:** Selecting a shape like "Hill" or "Valley" will dynamically reveal specific height/depth parameters.
3.  **Adjust Resolution:** Select a **Sub-square Size** from the presets (powers of 2) or use **Advanced Mode** for custom X/Y resolutions.
4.  **Generate:** Click **GENERATE** to process the math and see the 3D preview.
5.  **Export:** If the preview looks correct, click **EXPORT .MAP**. You can choose to override the existing map or save it as a new file.

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
| `--shape` | `hill`, `crater`, `ridge`, `slope`, `volcano`, `valley`, `flat` | The base macro shape |
| `--shapeheight` | `Number` | The height/depth of the macro shape landform |
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
