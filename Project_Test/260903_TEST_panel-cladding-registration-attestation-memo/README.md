# Panel Cladding Registration Attestation Memo Test

Checks that `AGENTS.md` forbids production registry activation from a potentially virtualized agent
context and requires independent host plus post-start attestation. It also checks that the inaccurate
1.0.70 production validation claims were retracted.

```powershell
powershell -ExecutionPolicy Bypass -File .\Project_Test\260903_TEST_panel-cladding-registration-attestation-memo\Test-PanelCladdingRegistrationAttestationMemo.ps1
```
