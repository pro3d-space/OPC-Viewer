## 1.1.9
- fixed sky dist coloring

## 1.1.8
- BVH performance improvements
- update README.md

## 1.1.7
- experimental embree support (disabled by default, use --embree)
- experimental closest point diff (toggle with M key)

## 1.1.6
- fixed deployed release artifacts

## 1.1.5
- MAJOR: Migrated from local Aardvark.Data.Remote project to official NuGet package v1.0.0
- Removed ~200 local files (source code, tests, examples, build artifacts)
- Updated project references and build system to use NuGet package
- Simplified test suite - no longer includes Aardvark.Data.Remote tests (handled by package)
- All PRo3D.Viewer functionality unchanged - seamless migration
- Added version display in window title (shows current version from RELEASE_NOTES.md)
- Added aardpack tool for automated GitHub releases with cross-platform binaries
- Added GitHub Actions workflow for automated releases on RELEASE_NOTES.md changes
- Updated build scripts with publish commands for standalone executables (Windows/Linux/macOS)
- Repository cleanup: moved tests to /src/, scripts to /ai/, runtime directories to /tmp/ (35% reduction in root items)

## 1.1.4
- Code polish: Improved naming conventions (FetchConfig.defaultConfig → Fetch.defaultConfig)
- Code polish: Complete migration from ResolverConfig to FetchConfig (eliminated legacy type)
- Code polish: Simplified provider module structure, removed unnecessary nested references
- Code polish: Optimized async patterns, removed unnecessary wrapper functions
- Maintained 0 errors, 0 warnings throughout all changes
- All 37 tests continue to pass, no functionality changes

## 1.1.3
- MAJOR: Refactored Aardvark.Data.Remote to idiomatic F# functional design + aligned C# config-based API
- Replaced C#-style builder pattern with clean F# record-based configuration
- F# API: resolve, resolveWith, resolveAsync, resolveAsyncWith, resolveMany, resolveManyWith 
- C# API: Resolve, ResolveWith, ResolveAsync, ResolveAsyncWith, ResolveMany, ResolveManyWith
- Converted provider system from interfaces to pure functional records
- Removed all mutable state - providers now immutable, no initialization needed
- All tests pass - complete functional implementation with 0 errors, 0 warnings

## 1.1.2
- Implemented remote data support for list command 
- Added configuration-based execution pattern to list command (matches view/diff/export)
- List command now supports HTTP/HTTPS URLs and SFTP sources with authentication
- List command supports multiple directories and remote sources in single operation
- Fixed README documentation for export JSON format (now uses unified `data` array)
- Added comprehensive documentation for unified data array format across all commands
- Updated list command to support all remote data features (verbose logging, force download, base directory)

## 1.1.1
- Removed experimental GoldenLayout web interface code that was accidentally mixed with legitimate features
- Replaced custom TriangleTree with optimized Uncodium.Geometry.TriangleSet library
    - fixes memory issues with large datasets (unbounded triangle splitting → bounded O(n) usage)
    - improves build performance from O(n²) worst-case to O(n log n) BVH construction  
    - adds SIMD-accelerated ray intersections (~1.9x throughput improvement)
    - resolves stack overflow and hang issues with problematic datasets
    - maintains complete API compatibility via adapter pattern
- Added remote data support to export command with full TDD test coverage
    - supports SFTP URLs with FileZilla XML authentication
    - supports HTTP/HTTPS zip file downloads  
    - merges multiple data sources into single export
    - added `--verbose` flag for detailed logging
    - exports now use same data resolution as view/diff commands
- Fixed export project property consistency (now uses `data` array like other commands)
- Added `--force-download` flag to replace cached data
- Added comprehensive tool invocation documentation to CLAUDE.md
- Cleaned up duplicate code, refactored Zip module API
- Extracted Aardvark.Data.Remote library with unified 'Fetch' API
    - added automatic corruption detection with transparent recovery
    - eliminated code duplication between HTTP and SFTP providers
    - added continuous progress reporting for HTTP and SFTP downloads with rate limiting
- Fixed Argu expression-based query error in project files
- Fixed wireframe mode (F key) to keep text overlays solid in Diff mode
- Disabled L-key in Diff mode (LoD visualization not applicable to difference triangles)

## 1.1.0
- .obj support
- JSON project file support
- Screenshots (F12)
- Configurable background colors (hex, RGB, named colors)
- Improved error handling and validation
- Improved documentation
- Tests

## 1.0.2
- Export to `.ply` format
- Export to `.pts` format  
- List datasets with `-s` shows statistics
- Support for reading `.obj` files
- Add RELEASE_NOTES.md for versioning