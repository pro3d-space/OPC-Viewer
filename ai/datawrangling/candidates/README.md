# OPC AI vs Regular Reconstruction Data Wrangling

This directory contains tools and data for processing Mars OPC datasets comparing AI-enhanced vs regular reconstruction.

## Files

- `list.json` - Structured list of 34 OPC pairs with verified SFTP paths
- `download.fsx` - F# script to download and cache all SFTP files locally
- `cache/` - Local cache directory for downloaded OPC files (gitignored)
- `CLAUDE.md` - Detailed documentation of findings and process

## Usage

### Download Script

The `download.fsx` script uses Aardvark.Data.Remote to efficiently cache all SFTP files:

```bash
# Run from ai/datawrangling/candidates/ directory
dotnet fsi download.fsx
```

**Features:**
- Uses Aardvark.Data.Remote's Fetch API exclusively
- Built-in caching - files downloaded once and reused automatically
- Natural SFTP path structure preservation  
- Console progress reporting via Fetch API
- Handles authentication via FileZilla XML config
- Automatic retry logic and error handling

**Requirements:**
- SFTP config file must exist: `W:\Datasets\Pro3D\confidential\2025-02-24_AI-Mars-3D\Mastcam-Z.xml`
- Network access to `dig-sftp.joanneum.at:2200`

### Cache Directory

Files are downloaded to `cache/` preserving the natural SFTP structure:
```
cache/sftp/dig-sftp.joanneum.at/Mission/0300/0320/
├── Job_0320_8341-034-rad/result/Job_0320_8341-034-rad_opc.zip
└── Job_0320_8341-034-rad-AI/result/Job_0320_8341-034-rad-AI_opc.zip
```

This structure:
- Preserves the source organization
- Allows safe cache sharing between scripts
- Uses Aardvark.Data.Remote's built-in caching logic

The cache directory is excluded from git to avoid storing large binary files in the repository.

## Data Overview

- **Total Pairs**: 34 OPC datasets
- **Sol Range**: 300-1300 (Mars mission days)
- **File Size**: ~68 files, estimated several GB total
- **Source**: dig-sftp.joanneum.at JOANNEUM RESEARCH server

## Integration

The cached files can be used with PRo3D.Viewer for:
- Batch diff comparisons between AI and regular reconstructions
- Performance analysis of AI enhancement techniques
- Validation of reconstruction quality improvements