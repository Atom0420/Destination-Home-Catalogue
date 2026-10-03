# Source checkpoint

The current repository preserves the October 3 appearance update from the source accompanying the published catalogue output. All eight C# documents in that output's portable PDB match the inspected source checksums, including the new appearance, interactive-control and content-control files. The original workspace is unchanged.

The independent project retains `DestinationHome.Catalogue` namespace/executable identity. Namespace references and embedded icon/artwork resource names are adapted together. Update and packaging scripts use the standalone repository root and executable names. Source packaging excludes private Git/IDE state and local downloaded data.

New source includes six palettes, appearance preferences, custom controls, detail scrolling, DPI handling, icon generation and packaging helpers. Original icon assets and their supplied provenance/prompt are preserved. Existing metadata models/service and update behavior are unchanged apart from the previously established standalone identity.

Matching local theme screenshots and the original extended offline QA record are preserved in `docs/images/themes` and `docs/history`. Generated binaries, PDBs, archive packages, metadata databases, image collections, preferences and logs remain excluded. No historical commits or release tags have been fabricated.
