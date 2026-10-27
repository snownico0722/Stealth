# Stealth 0.9.1 — Compact window list

- Move live window statistics to the top-right of the menu bar: **Showing N of M windows** / **显示 N / 共 M 个窗口**.
- Counts update whenever a title filter, closed/empty-title toggle or refresh changes the visible list.
- Merge File, Language and Help into one **Settings / 设置** menu with language switching, About and Exit.
- Keep window titles constrained to the available card width. Show up to **two lines**, then a character ellipsis; hover to see the full title.
- Disable horizontal window-list scrolling and keep vertical scrolling / virtualization.
- Remove the redundant bottom status bar, leaving more room for windows.
- Keep the single-file `Stealth.exe` packaging and .NET Framework 4.7.2 compatibility.

This is an unsigned Windows EXE. CI builds, unit tests and runs the standalone executable before publishing.
