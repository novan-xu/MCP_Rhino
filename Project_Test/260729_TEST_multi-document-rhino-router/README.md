# Multi-document Rhino Router test

Run the deterministic protocol smoke without Rhino:

```powershell
dotnet run --project Project_Test/260729_TEST_multi-document-rhino-router/RouterProtocolSmoke/RouterProtocolSmoke.csproj -c Debug
```

It starts concurrent fake current-user named-pipe MCP endpoints and validates pagination, private
attestation, stale/corrupt discovery, exact path routing, ambiguous paths, selection conflicts, and
independent per-Router selections. The Server developer command slug is
`multi-document-rhino-router-smoke-test`; the corresponding Rhino command is
`_McpMultiDocumentRhinoRouterSmoke`.

Live startup and document lifecycle checks require the packaged Release `.rhp`, saved disposable
fixtures, and Rhino 8. Never run mutation validation against production `.3dm` files.
