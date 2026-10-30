# Design

The wheel appears centered on the cursor within 100 ms of the hotkey, shows up to 8 slices, and is dismissed by a click, Esc, or moving away; every action ends with a small confirmation toast.

## Wheel variants

Variant C is chosen: groups on an inner ring, the hovered group's tools in an outer arc. Variants A (classic ring, 8 slices), B (floating bubbles) and D (half-moon arc) were considered and are kept in the original spec for reference. All variants follow the Windows light or dark theme.

C reaches every tool in two moves without opening a second window, and scales to many tools per group.

## Interaction flow

1. User optionally selects text in any app.
2. User presses the hotkey (default Ctrl+Alt+Space).
3. DevWheel captures the selection (if any), then shows the wheel at the cursor.
4. Hovering a slice highlights it and shows its label plus a one-line hint in the center hub.
5. Click (or release the hotkey in hold mode) runs the tool.
6. Groups with several tools (Encode, Hash, Generate) open an outer ring submenu on hover.
7. Result is pasted over the selection or copied to the clipboard; a toast confirms, e.g. "Base64 encoded, 248 chars".
8. Esc, right-click, or clicking outside closes the wheel with no action.

## Two trigger modes (setting)

- **Tap mode:** tap the hotkey, wheel stays open, click a slice.
- **Hold mode:** hold the hotkey, move toward a slice, release to run. Faster once learned, like game radial menus.

## Wheel layout

Variant C (chosen):

- **Inner ring:** 6 group slices of 60 degrees each, in fixed order clockwise from the top: Encode, Inspect, Format, Hash, Generate, Crypto. Inner radius about 56 px, outer about 128 px.
- **Outer arc:** appears when a group is hovered (120 ms fade), centered on that group's angle; one segment of about 24 degrees per tool, radius about 136 to 208 px. Groups with more than 6 tools widen the arc rather than shrink segments.
- **Highlight:** hovered group gets a light accent fill; hovered tool a stronger accent fill and 2 px border.
- **Hub:** shows the hovered tool's name and one hint line, e.g. "clears in 30 s" for sensitive tools; no tool is preselected when the wheel opens; with nothing hovered it shows "No selection" or the selection length.
- **Selecting:** moving from a group outward into the arc keeps the group open; moving back inside switches groups. Click on a tool runs it; click on a group with a single tool runs that tool.
- Tools that need a selection are dimmed when nothing was selected.
- Settings is not on the wheel; it lives in the tray menu.
- Sizes scale with Windows DPI. Near a screen edge the wheel shifts so the outer arc stays on the cursor's monitor.

## Keyboard support

- Number keys 1 to 6 pick a group, then 1 to n pick a tool in its arc; arrow keys rotate the highlight; Enter runs; Backspace returns to the groups.
- Each tool can optionally get its own direct hotkey (e.g. Ctrl+Alt+G for a GUID) to skip the wheel.

## Visual style

- Follows Windows light or dark theme; semi-transparent acrylic-like background, accent color on hover.
- Sensitive tools (keys, passwords, AES) get a small lock badge so users know the clipboard will auto-clear.
- Animations under 150 ms; respect the Windows "show animations" accessibility setting.

## Prompts and viewers

- **Key prompt** (HMAC, AES): small modal near the cursor with a masked field, never remembered between uses.
- **Generator options** (secret length, format): defaults from settings; Shift+click on a slice opens the options dialog instead of using defaults.
- **JWT and timestamp viewer:** small read-only window with formatted JSON, expiry shown in local time and as "expires in 2 h", plus a Copy button.

## Feedback and errors

- Success toast near the cursor for 1.5 s.
- Errors shown in the same toast in red with a plain message, e.g. "Not valid Base64". The original text is never changed on error.
- Sensitive outputs: toast says "Copied, clears in 30 s".

## Tray and settings

Tray icon menu: Open settings, Pause hotkey, About, Exit. The settings window covers:

- Hotkey (default Ctrl+Alt+Space): click "Record", press the new combination, see it confirmed or rejected as taken; "Reset to default" button. Also the trigger mode (tap or hold)
- Which groups and tools appear on the wheel, and their order
- Output preference per tool: replace selection or clipboard only
- Clipboard auto-clear time (default 30 s, off not allowed for sensitive tools)
- Generator defaults (length, encoding)
- Start with Windows
