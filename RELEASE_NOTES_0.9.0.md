# Stealth 0.9.0 — Single EXE / 单文件版

- `Stealth.exe` now embeds its managed dependencies using Costura.Fody.
- Retains .NET Framework 4.7.2 / WPF, Simplified Chinese, window opacity and Always on Top functionality.
- Download and run the EXE directly; no companion DLLs or ZIP extraction.
- SHA256SUMS.txt provides the official release binary checksum.

**Requirements:** Windows with .NET Framework 4.7.2 or newer installed. The framework runtime itself is **not** bundled. The EXE is not Authenticode signed.

**CI:** Release compilation, tests and clean-directory launch smoke test.
