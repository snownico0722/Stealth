# Stealth v0.10.0 — Window reliability update / 窗口可靠性更新

Stealth remains a **single unsigned `Stealth.exe`** with its managed libraries embedded. Windows with **.NET Framework 4.7.2 or later** is required.

## Fixes / 修复

1. **Reversible window modifications:** record original opacity, layered attributes and always-on-top state when first modifying a window. Restore on normal exit or through a new per-window right-click **Restore original window state / 恢复窗口原始状态** action. Refuse unsafe changes to windows whose original layered transparency cannot be read.
2. **Closed window history:** automatically discard unmodified closed windows, keep modified historical entries read-only, and avoid treating reused window handles as the same window.
3. **Always-on-top validation:** verify `SetWindowPos` and read back the actual state; failed operations revert the UI checkbox rather than reporting false success.
4. **Process icon cache:** cache application icons across windows and refreshes, discard dead process entries, and safely reuse frozen WPF image sources.
5. **Long window titles:** dynamically allocate up to 16,383 UTF-16 characters instead of truncating at 255.
6. **Remove unused `GetWindowInfo` call:** the API was previously called with uninitialized `cbSize`. Keep the CsWin32 metadata generator entry because other style definitions require it.
7. **Responsive controls:** options wrap on narrow windows and the list remains horizontally non-scrollable.
8. **Two-line ellipsis:** replace height-clipped `TextBlock` with a DPI-aware WPF `FormattedText` renderer limited to two lines. Retain the full title in the tooltip and capture a raster sample during CI.
9. **Mixed DPI handling:** place the floating overlay in physical screen pixels and constrain its position to the selected monitor's working area; react to DPI changes.
10. **Event-driven overlay:** use WinEvent hooks with event coalescing and a 1.5-second fallback, replacing 150-ms continuous polling.
11. **Overlay off by default:** first-time installs do not show or monitor a floating overlay, while existing users' saved preferences are honored.

## Validation / 验证

GitHub Actions builds Windows Debug and Release, runs unit tests and real Win32 window-state integration tests, renders a WPF title regression bitmap, starts the lone EXE from a clean directory, verifies the release checksum, and publishes only after all steps succeed.

Cross-monitor mixed-DPI positioning, accessibility interactions, unusual applications and forced-termination state restoration **still require manual Windows testing**. Changes to other programs are not guaranteed reversible after an unclean forced termination; if another process changes window properties concurrently, Stealth avoids overriding it.

The EXE is not Authenticode code-signed. Legacy .NET Framework 4.7.2 WPF compatibility is preserved.
