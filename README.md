# OPC-Viewer

Simple viewer for Ordered Point Cloud Data for experimenting with data and processing.

<img src="https://github.com/user-attachments/assets/327d99d3-451c-4dd4-bf5e-0565ffbb47d2" width="25%" />



Datasets can be downloaded [from the pro3d website](https://pro3d.space), e.g. [the victoria crater dataset](http://download.vrvis.at/acquisition/32987e2792e0/PRo3D/VictoriaCrater.zip).

## Quickstart

```
git clone https://github.com/pro3d-space/OPC-Viewer
cd OPC-Viewer
build
opcviewer view http://download.vrvis.at/acquisition/32987e2792e0/PRo3D/VictoriaCrater.zip
```

## Command Line Tool

```
$ opcviewer --help
USAGE: PRo3D.OpcViewer [--help] [--version] [<subcommand> [<options>]]

SUBCOMMANDS:

    diff <options>        Compute difference between a layer with other layers.
    export <options>      Export data from datasets.
    list, ls <options>    List datasets.
    view <options>        View datasets.

    Use 'PRo3D.OpcViewer <subcommand> --help' for additional information.

OPTIONS:

    --version             Print program version.
    --help                display this list of options.
```

## List

List all datasets in a directory:
```
$ opcviewer ls "D:\Pro3D\VictoriaCrater"
D:\Pro3D\VictoriaCrater\HiRISE_VictoriaCrater\OPC_000_002
D:\Pro3D\VictoriaCrater\HiRISE_VictoriaCrater\OPC_001_002
D:\Pro3D\VictoriaCrater\HiRISE_VictoriaCrater_SuperResolution\OPC_000_000
D:\Pro3D\VictoriaCrater\MER-B_CapeDesire_wbs\OPC_000_000
```

Use `-s` to show additional statistics for each dataset:
```
$ opcviewer ls -s "D:\Pro3D\VictoriaCrater"
D:\Pro3D\VictoriaCrater\HiRISE_VictoriaCrater\OPC_000_002
    Positions, DiffuseColorCoordinates
    63 nodes (42 leafs, 21 inner, levels 0..9)
    vertices    2 774 058
    faces       5 505 024
...
```

## Diff

Visualize geometric distance between two layers.

```
opcviewer diff <LAYER1> <LAYER2>
```

| Key | Description |
| --- | ----------- |
| `T` | toggle between layers |
| `C` | color texture on/off |
| `F` | wireframe on/off |
| `W`, `A`, `S`, `D` | move forward, left, right, back |

## Export
Create `.pts` or `.ply` files from datasets.

```
$ opcviewer export D:\Pro3D\VictoriaCrater\HiRISE_VictoriaCrater\OPC_001_002 --format pts --out test.pts
wrote 3329842 points to test.pts
```

## Specifying Datasets

The `opcviewer` tool can load datasets from local directories, but also remotely from `HTTP`, `HTTPS` and `SFTP` locations.

If specifying a remote location (see *Quickstart* for an example), then it is expected that the given URL references a **.zip file**.
The .zip file will be downloaded and extracted to a local cache folder. Files are downloaded only once. If the same remote URL is used again, then the locally cached content will be used without downloading the file again.

### SFTP
If the server requires authentication, then you can specify a config file in FileZilla format using `--sftp`, e.g.

```
opcviewer view "sftp://alice@sftp-server.example.org:2200/ex/ample/dataset.zip" --sftp "path/to/filezilla-config.xml" 
```
