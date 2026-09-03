# Results — 2026-09-03

The focused contract test passed:

```powershell
powershell -ExecutionPolicy Bypass -File .\Project_Test\260903_TEST_panel-cladding-registration-attestation-memo\Test-PanelCladdingRegistrationAttestationMemo.ps1
```

It confirms that `AGENTS.md` now requires:

- stage-only behavior when host-persistent registry access is uncertain;
- an independent host-process read after the installer exits;
- exact `PlugIn\FileName`, command-list, existence, and timestamp checks;
- post-start loaded-module and timestamp verification; and
- restoration of the prior reachable RHP on failure.

It also confirms that the inaccurate production-attestation claims in the 1.0.70 RESULTS and EXET
records were retracted. `git diff --check` passed with line-ending notices only.

An independent `reg.exe` read after the user's real-hive repair showed the existing 1.0.70 RHP and
all eleven commands including `PCUpdate`.
