# Stone Wireframe Bake Test

## Coverage

- strict assembly, flat-stone, vertical-stone, and excluded-suffix naming
- preview/apply MCP method safety annotations
- CLI fallback returns `LIVE_RHINO_REQUIRED`
- live preview resolves assemblies, nested stones, upper horizontal lines, and output placements without mutation
- production tool discovery through normal assembly registration
- Debug and Release MCP safety and build checks
- installed Router preview/apply and second-run idempotency on the target model

## CLI Slug

`stone-wireframe-bake-smoke-test [active-saved-3dm-path]`

## Rhino Command

`_McpStoneWireframeBakeSmoke`

The Rhino command runs preview only. Production application is validated separately through the installed
Router and remains protected by the capability's single undo record.
