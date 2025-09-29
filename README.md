# OPC-Viewer

Simple viewer for Ordered Point Cloud Data for experimenting with data and processing.

<img src="https://github.com/user-attachments/assets/327d99d3-451c-4dd4-bf5e-0565ffbb47d2" width="25%" />



Datasets can be downloaded [from the pro3d website](https://pro3d.space), e.g. [the victoria crater dataset](http://download.vrvis.at/acquisition/32987e2792e0/PRo3D/VictoriaCrater.zip).

## Quickstart

```
git clone https://github.com/pro3d-space/OPC-Viewer
cd OPC-Viewer
build
pro3dviewer view http://download.vrvis.at/acquisition/32987e2792e0/PRo3D/VictoriaCrater.zip
```

## Command Line Tool

```
$ pro3dviewer --help
USAGE: PRo3D.Viewer [--help] [--version] [--dryrun] [--screenshots <path>] [<subcommand> [<options>]]

SUBCOMMANDS:

    view <options>        View datasets.
    diff <options>        Compute difference between two layers.
    list, ls <options>    List datasets.
    export <options>      Export data from datasets.
    project <options>     Load configuration from JSON project file.

    Use 'PRo3D.Viewer <subcommand> --help' for additional information.

OPTIONS:

    --version             Print program version.
    --dryrun              Parse arguments and output as JSON without executing commands.
    --screenshots <path>  Custom directory for saving screenshots (default: ./screenshots).
    --help                display this list of options.
```

## Common Options

Many commands share these options:

| Option | Short | Description |
| ------ | ----- | ----------- |
| `--sftp <file>` | `-s` | SFTP server config file (FileZilla format) |
| `--base-dir <path>` | `-b` | Base directory for relative paths (default: ./data or ./tmp/data) |
| `--force-download` | `-f` | Force re-download of remote data even if cached |
| `--verbose` | `-v` | Print detailed info |
| `--background-color <color>` | `--bg` | Color: hex (#RGB/#RRGGBB), named (black/white/red), or RGB (r,g,b) |

## View

```
pro3dviewer view <DATASET>...
pro3dviewer view <DATASET>... --obj <FILE>...
```

**Specific options:**
- `--obj <FILE>` / `-o` - Load OBJ models alongside OPC data
- `--speed <FLOAT>` - Camera movement speed

**Supports:** `--sftp`, `--base-dir`, `--background-color`, `--force-download`, `--verbose`

## Diff

```
pro3dviewer diff <LAYER1> <LAYER2>
```

**Specific options:**
- `--noValue <FLOAT>` - Value for no difference (default: NaN)
- `--speed <FLOAT>` - Camera movement speed
- `--embree` - Use Embree backend for triangle intersection (Windows only)

**Supports:** `--sftp`, `--base-dir`, `--background-color`, `--force-download`, `--verbose`

## List

```
pro3dviewer ls <DIRECTORY>...
pro3dviewer ls <DIRECTORY>... --stats
```

**Specific options:**
- `--stats` / `-s` - Show attributes, node count, vertex/face count

**Supports:** `--sftp`, `--base-dir`, `--force-download`, `--verbose`

## Export

```
pro3dviewer export <DATASET>... --format <pts|ply> --out <FILE>
```

**Required options:**
- `--format <pts|ply>` - Export format
- `--out <FILE>` - Output filename

**Supports:** `--sftp`, `--base-dir`, `--force-download`, `--verbose`

## Keyboard Shortcuts

| Key | Description | View | Diff |
| --- | ----------- | :--: | :--: |
| `W`, `A`, `S`, `D` | Move forward, left, backward, right | ✓ | ✓ |
| `PageUp` / `PageDown` | Increase/decrease movement speed | ✓ | ✓ |
| `F` | Toggle wireframe/fill mode | ✓ | ✓ |
| `L` | Toggle Level-of-Detail visualization | ✓ |   |
| `F12` | Save screenshot | ✓ | ✓ |
| `T` | Toggle between layers |   | ✓ |
| `C` | Toggle distance visualization |   | ✓ |
| `M` | Toggle distance computation mode (Sky/Nearest) |   | ✓ |

## Dry Run Mode

The `--dryrun` flag parses command-line arguments and outputs them as JSON without executing the command. Useful for testing argument parsing and creating project files.

```bash
$ pro3dviewer --dryrun view data1 data2 --speed 5.0
{
  "command": "view",
  "data": [
    { "path": "data1" },
    { "path": "data2" }
  ],
  "speed": 5
}
```

## Project Files

Instead of command-line arguments, use JSON project files:

```bash
pro3dviewer project config.json
pro3dviewer project config.json --force-download
# or shortcut:
pro3dviewer config.json
pro3dviewer config.json -f
```

### Project File Reference

#### Common Fields

| Field | Type | Description | Commands |
| ----- | ---- | ----------- | -------- |
| `command` | string | Command to execute | All |
| `data` | array | Data sources (see Data Format below) | All |
| `sftp` | string | Path to FileZilla SFTP config | All except diff |
| `baseDir` | string | Base directory for relative paths | All except view |
| `forceDownload` | bool | Force re-download cached data | All except list |
| `verbose` | bool | Enable verbose output | view, diff, export |
| `screenshots` | string | Screenshot directory path | view, diff |
| `backgroundColor` | string | Background color | view, diff |

#### Command-Specific Fields

**View:**
- `speed` (float) - Camera movement speed
- `cameraOutlierPercentile` (float) - Outlier trimming for camera positioning (default: 2.5)

**Diff:**
- `noValue` (float) - Value for no difference (default: NaN)
- `speed` (float) - Camera movement speed
- `useEmbree` (bool) - Use Embree backend (Windows only)
- `cameraOutlierPercentile` (float) - Outlier trimming for camera positioning

**List:**
- `stats` (bool) - Show detailed statistics

**Export:**
- `format` (string) - "pts" or "ply" (required)
- `out` (string) - Output filename (required)

### Data Format

The `data` field accepts different formats per command:

#### View/Export - Full data entries with transforms:
```json
{
  "data": [
    {
      "path": "dataset1",
      "type": "opc",
      "transform": "[[1, 0, 0, 10], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]"
    },
    {
      "path": "model.obj",
      "type": "obj"
    }
  ]
}
```

**Data Entry Properties:**
- `path` (required) - File path, directory, or URL
- `type` (optional) - "opc" or "obj" (auto-inferred from extension)
- `transform` (optional) - 4x4 transformation matrix as string

#### Diff/List - Simple string array:
```json
{
  "data": ["path1", "path2"]
}
```

### Example Projects

#### View Project
```json
{
  "command": "view",
  "data": [
    { "path": "dataset1", "type": "opc" },
    { "path": "model.obj", "type": "obj" },
    { "path": "terrain.obj" }
  ],
  "speed": 2.0,
  "backgroundColor": "#000080",
  "screenshots": "./project-screenshots",
  "verbose": true
}
```

#### Diff Project
```json
{
  "command": "diff",
  "data": ["layer1", "layer2"],
  "verbose": true,
  "noValue": "NaN",
  "backgroundColor": "white",
  "useEmbree": true,
}
```

#### List Project
```json
{
  "command": "list",
  "data": ["dir1", "dir2", "http://server.com/data.zip"],
  "stats": true,
  "verbose": true,
  "forceDownload": false
}
```

#### Export Project
```json
{
  "command": "export",
  "data": [
    { "path": "dataset1", "type": "opc" },
    { "path": "dataset2", "type": "opc" }
  ],
  "format": "pts",
  "out": "output.pts",
  "verbose": true,
  "forceDownload": false,
  "sftp": "path/to/config.xml"
}
```

## Specifying Datasets

The `pro3dviewer` tool can load datasets from local directories, but also remotely from HTTP, HTTPS and SFTP locations.

**Supported paths:**
- Local directories: `./data`, `C:\Data`
- Local files: `model.obj`, `data.zip`
- HTTP/HTTPS URLs: `https://example.com/data.zip`
- SFTP URLs: `sftp://user@server.com/path/data.zip`

Remote .zip files are downloaded and extracted to a local cache. Files are cached and reused unless `--force-download` is specified.

### SFTP Authentication

For servers requiring authentication, specify a FileZilla config file:

```
pro3dviewer view "sftp://alice@sftp-server.example.org:2200/ex/ample/some_opc.zip" --sftp "path/to/filezilla-config.xml"
```