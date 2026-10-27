# Stealth 0.10.1 — 撤回未经要求的操作限制

This patch release contains **only the rollback of additional restrictions**, with the requested v0.10.0 window reliability improvements retained.

## Reverted / 已撤回

- Stealth no longer blocks operations on its own windows or windows with unidentified process ownership.
- When layered-window attributes cannot be read, Stealth again attempts the requested opacity change and reports the best-effort warning rather than refusing the operation.
- An opacity change made by another application no longer blocks later user-initiated opacity changes in Stealth.
- Both the main opacity slider and floating overlay now allow the original full alpha range, **0–255**.

The user-requested changes to restoration, window history, topmost verification, icon caching, title length, adaptive UI, DPI positioning, event handling and the overlay's default-off setting remain in place.

**Known limitation:** A special layered window whose original transparency attributes cannot be read may not be restorable exactly; allowing adjustment does not make the original unknown data recoverable.

No bundled runtime change: this is still one `Stealth.exe` requiring Windows .NET Framework 4.7.2 or later. The binary is unsigned.
