# CLIPALKA — Windows capture studio

Native C# / WPF, .NET 8. Direction: quiet Fluent-inspired dark desktop workspace,
informed by the user's preferred purple capture dashboard. This is an implemented
application theme, not a marketing page or an image used in place of controls.

## Decisions

- Graphite canvas #0C0D12, sidebar #111218, content #0E0F15.
- Raised surface #191A22, input #22242E, boundary #343744.
- Primary text #F5F5FA, secondary #B1B2C4, muted #989BAD.
- Replay accent #8055EA, accent text #AD91FF, focus #C2AAFF.
- Record #FF506C, capture ready #4DE3B1, always paired with a text state.
- Segoe UI Variable Text / Segoe UI. Page 32, source 20, section 16,
  controls 14, secondary 13, compact metadata 11–12 DIPs.
- 4/8 spacing rhythm; cards 14 radius, inputs/buttons 8, preview 10.
- One primary action: save the last 30 seconds. Record/microphone are secondary
  circular controls with aligned labels and keyboard shortcut chips.
- Fixed 64-DIP title bar, 216/184 sidebar, 384/326 settings rail. Below 1180 DIPs
  the rail moves underneath capture. Minimum window 960×640; initial size is
  clamped to the Windows work area. One vertical scroll; text is never scaled down.
- No fake live preview, HDR setting, unsupported FPS or decorative telemetry.
  The preview is the last saved clip; the empty state says when it appears.
- One vector icon family and a small vector CP mark; no emoji navigation.
- Shared theme: src/Clipalka.App/Themes/Studio.xaml. The same tokens style the
  gallery, recording/settings pages, notifications and recording indicator.

## Interaction

Keyboard-visible focus, selected navigation marker and full selected device names
in tooltips. ComboBox popup uses a dark item template and visibly selected item.
Buttons preserve their primary color on hover. Disabled controls cannot execute.
No looping UI animation. Toast motion follows Windows ClientAreaAnimation.
Global hotkey behavior and native capture/audio algorithms remain in their services.

## ui-ux-pro-max application

Applied the verified WPF guidance for resource dictionaries, keyboard navigation,
semantic controls and AutomationProperties. Applied contrast and focus guidance.
The initial gaming design-system search overemphasized 3D/marketing layouts;
the refined desktop-utility search matched layered dark surfaces. Its marketing
sections, green CTA and web fonts were not adopted: the user reference and Windows
desktop context call for a purple replay action and native typography.

## Verification

Render the real WPF visual tree on Windows at 1585×992, 1280×800, 1180×760, 1024×768,
and 150% DPI,
plus gallery, recording, controller settings, empty/recording/error states,
device popup and notification variants. Check shortcut baselines and action
bounds. Rendering fixtures must never enter normal startup or save user settings.
Keep native recorder hash/load checks and upgrade/export checks passing.
Do not publish a release automatically for a design iteration.
