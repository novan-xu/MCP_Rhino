# Local Rhino Test Files

Rhino `.3dm` fixtures are intentionally excluded from version control because they can contain project geometry, document metadata, render paths, and workstation details.

Place developer-owned fixtures under `Runtime_Test/local/` when running live smoke tests. The repository keeps test code and fixture instructions, but never commits the model files themselves.
