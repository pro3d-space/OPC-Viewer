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

    diff <options>        Compute difference between a layer with other layers.
    export <options>      Export data from datasets.
    list, ls <options>    List datasets.
    project <options>     Load configuration from JSON project file.
    view <options>        View datasets.

    Use 'PRo3D.Viewer <subcommand> --help' for additional information.

OPTIONS:

    --version             Print program version.
    --dryrun              Parse arguments and output as JSON without executing commands.
    --screenshots <path>  Custom directory for saving screenshots (default: ./screenshots).
    --help                display this list of options.
```

## List

List all datasets in a directory:
```
$ pro3dviewer ls "D:\Pro3D\VictoriaCrater"
D:\Pro3D\VictoriaCrater\HiRISE_VictoriaCrater\OPC_000_002
D:\Pro3D\VictoriaCrater\HiRISE_VictoriaCrater\OPC_001_002
D:\Pro3D\VictoriaCrater\HiRISE_VictoriaCrater_SuperResolution\OPC_000_000
D:\Pro3D\VictoriaCrater\MER-B_CapeDesire_wbs\OPC_000_000
```

Use `-s` to show additional statistics for each dataset:
```
$ pro3dviewer ls -s "D:\Pro3D\VictoriaCrater"
D:\Pro3D\VictoriaCrater\HiRISE_VictoriaCrater\OPC_000_002
    Positions, DiffuseColorCoordinates
    63 nodes (42 leafs, 21 inner, levels 0..9)
    vertices    2 774 058
    faces       5 505 024
...
```

## View

View OPC datasets and OBJ models.

```
pro3dviewer view <DATASET>
pro3dviewer view <DATASET> --background-color red
pro3dviewer view <DATASET> --bg #FF0000
```

## Diff

Visualize geometric distance between two layers.

```
pro3dviewer diff <LAYER1> <LAYER2>
pro3dviewer diff <LAYER1> <LAYER2> --bg white
```

## Keyboard Shortcuts

| Key | Description | View | Diff |
| --- | ----------- | :--: | :--: |
| `W`, `A`, `S`, `D` | move forward, left, right, back | ✓ | ✓ |
| `F` | toggle wireframe/fill mode | ✓ | ✓ |
| `L` | toggle Level-of-Detail visualization | ✓ | ✓ |
| `F12` | save screenshot to configured directory (default: ./screenshots) | ✓ | ✓ |
| `PageUp` | increase movement speed | ✓ | ✓ |
| `PageDown` | decrease movement speed | ✓ | ✓ |
| `T` | toggle between layers | | ✓ |
| `C` | toggle distance visualization | | ✓ |

## Export
Create `.pts` or `.ply` files from datasets.

```
$ pro3dviewer export D:\Pro3D\VictoriaCrater\HiRISE_VictoriaCrater\OPC_001_002 --format pts --out test.pts
wrote 3329842 points to test.pts
```

## Dry Run Mode

The `--dryrun` flag parses command-line arguments and outputs them as JSON without executing the command. This is useful for testing argument parsing and creating project files.

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

$ pro3dviewer --dryrun export mydata --format Pts --out output.pts
{
  "command": "export",
  "dataDir": "mydata",
  "format": "pts",
  "out": "output.pts"
}
```

## Project Files

Instead of command-line arguments, use JSON project files:

```bash
pro3dviewer project config.json
# or shortcut:
pro3dviewer config.json
```

### View Project

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
  "screenshots": "./project-screenshots"
}
```

With transformations:
```json
{
  "command": "view",
  "data": [
    {
      "path": "dataset1",
      "type": "opc",
      "transform": "[[1, 0, 0, 10], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]"
    },
    {
      "path": "model.obj",
      "transform": "[[2, 0, 0, 0], [0, 2, 0, 0], [0, 0, 2, 0], [0, 0, 0, 1]]"
    }
  ]
}
```

### Diff Project
```json
{
  "command": "diff",
  "data": ["layer1", "layer2"],
  "verbose": true,
  "noValue": "NaN",
  "backgroundColor": "gray"
}
```

### List Project
```json
{
  "command": "list",
  "data": ["dir1", "dir2", "dir3"],
  "stats": true
}
```

### Export Project
```json
{
  "command": "export",
  "dataDir": "dataset",
  "format": "pts",
  "out": "output.pts"
}
```

Paths in project files can be:
- Relative (resolved from project file location or baseDir)
- Absolute (`C:\Data` or `/home/data`)
- URLs (`https://example.com/data.zip`, `sftp://server/path`)

## Specifying Datasets

The `pro3dviewer` tool can load datasets from local directories, but also remotely from `HTTP`, `HTTPS` and `SFTP` locations.

If specifying a remote location (see *Quickstart* for an example), then it is expected that the given URL references a **.zip file**.
The .zip file will be downloaded and extracted to a local cache folder. Files are downloaded only once. If the same remote URL is used again, then the locally cached content will be used without downloading the file again.

### SFTP
If the server requires authentication, then you can specify a config file in FileZilla format using `--sftp`, e.g.

```
pro3dviewer view "sftp://alice@sftp-server.example.org:2200/ex/ample/some_opc.zip" --sftp "path/to/filezilla-config.xml" 
```
