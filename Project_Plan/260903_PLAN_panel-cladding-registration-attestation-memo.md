# Panel Cladding Registration Attestation Memo Plan

## 背景

PanelCladdingEditor 1.0.70 files were activated while the real Rhino registry still referenced the
removed 1.0.69 RHP. Installer and validation output produced inside the same agent environment was
mistaken for proof that the host-persistent registry had changed.

## 目标

- Add a concise hard rule to `AGENTS.md` distinguishing installer self-validation from independent
  host-persistence attestation.
- Prohibit reporting a production install as complete when the execution environment may virtualize
  registry writes.
- Prevent retirement of the prior active RHP until the real hive independently points to the new,
  existing file and exposes the exact command list.
- Require post-start timestamp and loaded-module verification.
- Correct the 1.0.70 execution and test records that overstated the failed production gate.

## 实施与验收

1. Extend the existing Rhino registration memo rather than create a competing rule section.
2. Require a fresh independent `reg.exe`/host-shell read after the installer process exits; a
   same-session readback is not sufficient when persistence is uncertain.
3. Require the old active directory to remain available until that attestation passes; otherwise
   stage only and report the install as unverified.
4. Require the next Rhino start to advance root/`CommandList` timestamps and load the exact RHP,
   while `PlugIn` remains installer-owned.
5. Add a focused static contract test and execution record.

## 回退

Revert the added memo paragraph and matching PLAN/TEST/EXET records. No Rhino model or production
registry mutation is part of this task.
