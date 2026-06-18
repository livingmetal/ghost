# Legacy Tree Cleanup

## Scope

The repository previously tracked a nested historical snapshot at:

```text
src/LivingMetalGhost/Ghost
```

The active project explicitly excluded this tree from compilation and content
copying.

## Inventory Result

The snapshot contained 251 tracked files.

- 187 files had a byte-identical blob elsewhere in the repository.
- 64 files had no byte-identical active match.
- Most unmatched text/code files were superseded implementations or obsolete
  scaffold files.
- Unique character-authoring material was preserved before deletion.

## Preserved Material

Moved to `authoring/Characters`:

- Orkia `BodyBases`;
- Orkia `FaceBases`;
- Orkia `HairStyles`;
- Orkia `HeadBases`;
- Orkia reference material, including the VRM sample;
- ssyong normalization documentation and scripts.

The authoring directory is intentionally outside
`src/LivingMetalGhost/Assets/Characters`, so these files are not included in
runtime build or publish output.

## Removed Material

- duplicate source code;
- duplicate runtime character assets;
- duplicate build and publish scripts;
- obsolete scaffold models and configuration;
- historical project files and documentation.

## Runtime Asset Cleanup

The active runtime character tree was audited after the legacy snapshot
removal. Runtime manifests did not reference `_work`, `_original`, `References`,
or `Rigging` directories.

Those directories were moved to `authoring/Characters`. Orkia offline scripts
were moved with their inputs and updated to separate authoring inputs from
runtime sprite outputs. Two tracked Python bytecode cache files were deleted.

The project file retains explicit exclusion guards for authoring directory
names in case they are accidentally reintroduced under runtime assets.
