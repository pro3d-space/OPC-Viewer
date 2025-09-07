# OPC AI vs Regular Reconstruction Candidates

This document contains findings from analyzing Mars OPC datasets for AI-enhanced vs regular reconstruction comparison.

## Data Sources

- **PDF Source**: `W:\Datasets\Pro3D\AI_Candidates_List.pdf` - Contains 34 pairs of OPC datasets across Sol 300-1300
- **Example Configuration**: `examples\diff-ai-comparison.json` - Working SFTP configuration for Job_0610_8618-110-rad
- **Output**: `ai\datawrangling\candidates\list.json` - Structured JSON with all 34 pairs and verified SFTP paths

## SFTP Server Details

- **Host**: `dig-sftp.joanneum.at:2200`
- **Username**: `mastcam-z` (not `mastcam-z-admin` as shown in example)
- **Authentication**: Available in `W:\Datasets\Pro3D\confidential\2025-02-24_AI-Mars-3D\Mastcam-Z.xml`
- **Connection Tested**: Successfully verified with psftp command-line tool

## Directory Structure Pattern

```
Mission/
├── [SOL_GROUP]/           # e.g., 0300, 0400, 0500, etc.
│   ├── [JOB_NUM]/         # e.g., 0320, 0349, 0461, etc.
│   │   ├── Job_[JOB_NUM]_[ID]-[TYPE]-rad/           # Regular reconstruction
│   │   │   └── result/
│   │   │       └── Job_[JOB_NUM]_[ID]-[TYPE]-rad_opc.zip
│   │   └── Job_[JOB_NUM]_[ID]-[TYPE]-rad-AI/        # AI-enhanced reconstruction
│   │       └── result/
│   │           └── Job_[JOB_NUM]_[ID]-[TYPE]-rad-AI_opc.zip
```

## Verified Path Examples

### Regular Reconstruction
```
sftp://mastcam-z@dig-sftp.joanneum.at:2200/Mission/0300/0320/Job_0320_8341-034-rad/result/Job_0320_8341-034-rad_opc.zip
```

### AI-Enhanced Reconstruction  
```
sftp://mastcam-z@dig-sftp.joanneum.at:2200/Mission/0300/0320/Job_0320_8341-034-rad-AI/result/Job_0320_8341-034-rad-AI_opc.zip
```

## Dataset Distribution

| Sol Range | Pairs | Notes |
|-----------|-------|--------|
| 300       | 4     | Early mission data |
| 400       | 4     | - |
| 500       | 3     | Some .zip extensions in names |
| 800       | 2     | - |
| 900       | 10    | Highest concentration |
| 1000      | 2     | - |
| 1100      | 4     | Includes BAA extra computations |
| 1200      | 2     | - |
| 1300      | 2     | Latest mission data |
| **Total** | **34** | |

## Special Cases

- **Sol 1100**: 
  - Job_1130_9150-079-rad: "to be repeated normal reconstruction"
  - Job_1177_9218-110-rad & Job_1178_9219-063-rad: "extra computed by BAA"

## Verification Process

1. **Connection Test**: Used psftp with credentials from Mastcam-Z.xml
2. **Directory Listing**: Verified Mission/ structure and Sol groupings
3. **Sample Inspection**: Confirmed Job_0320_8341-034-rad directory structure
4. **File Verification**: Located actual _opc.zip files in result/ subdirectories

## Key Findings

- Original example used different naming convention (`-AI-Test` vs `-AI`, `-NoAI` vs standard)
- All 34 pairs follow consistent directory structure
- Files are located in `result/` subdirectories, not directly in job directories
- Username correction critical for access (`mastcam-z` not `mastcam-z-admin`)

## Usage

The structured JSON file can be used to:
- Generate diff comparison project files
- Batch process AI vs regular reconstructions
- Create automated comparison workflows
- Validate SFTP accessibility before processing

## Command Line Verification

```bash
# Use actual password from W:\Datasets\Pro3D\confidential\2025-02-24_AI-Mars-3D\Mastcam-Z.xml
psftp mastcam-z@dig-sftp.joanneum.at -P 2200 -pw "[password_from_xml]" -batch -b commands.txt
```

Where commands.txt contains:
```
ls Mission/
ls Mission/0300/0320/
ls Mission/0300/0320/Job_0320_8341-034-rad/result/
quit
```