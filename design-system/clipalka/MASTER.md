# CLIPALKA — dark teal desktop

The latest user reference is the compact dark Dota2ProTracker interface. It
supersedes the earlier purple Studio styling. Keep the useful capture/gallery/
settings structure, using functional C# / WPF controls under .NET 8.

## Product structure

- A 48-DIP dark teal title bar: CLIPALKA left, three compact navigation tabs,
  native window actions right. Capture, My clips, Settings; no second sidebar.
- Capture: concise heading and real buffer state, large last-clip preview,
  three equal-height actions with actual English shortcut hints inside them.
- Source, audio and clips-folder quick links, followed by three recent clips.
- Gallery: three fluid columns with real thumbnails, ellipsized names with
  tooltips, saved date, duration, playback and reveal-in-folder actions.
- Settings: Video & files, Audio, Keyboard & controller; explicit Save action.
- Controller settings: device choice, learning, current inputs, saved binding,
  status and a separate game-only option. Explain blocked replay commands.
- Quiet footer for replay state and current operation details.

## Visual system

- Flat canvas #080D11, panel #101B23, input #152129, border #2B3E48.
- Title bar #0B252D, selected navigation #203F49.
- Replay #187D85, focus #9AEBE5, ready #6BD4BD, recording #FF506C.
- Text #ECF3F5, secondary #ABBDC6, tertiary #8EA4AF.
- Segoe UI Variable Text / Segoe UI. Page headings 25–28, actions 16–19,
  body 14–16, secondary labels 12–14. Never scale down the entire window.
- Compact 6–8-DIP corners, thin borders, no decorative glow or large gradients.
- 4/8 spacing rhythm, 16-DIP control gaps, 28-DIP page gutters.
- Stroke icons from the existing vector family; no missing font glyphs.
- Hover/pressed overlays cover the complete rounded control. Content padding
  belongs on the presenter, not the template root containing the hover layer.
- Keyboard focus remains visible. Selection is shown with surface and text.

## Functional details

Normal startup shows a real saved clip, never demo artwork or a fake live feed.
Source name, FPS, mute state and shortcut hints come from actual settings.
Preserve native PCM continuity, capture selection, Bluetooth input backends,
notifications and installer behavior.

The artwork in Assets/studio-demo.png is only used by --render-ui verification.
The older studio-reference.png remains as historical design exploration.

## Verification

Render real WPF at 1585×992, 1280×800, 1180×760, 1024×768 and 150% DPI.
Cover gallery, settings, empty, recording, error and muted states, device popup
and keyboard focus. Assert action boundaries, shortcut baselines and full-control
hover coverage. Capture and recent clips fit the default viewport.
Keep core, native audio, replay export, controller service and installer checks.
A physical Bluetooth controller remains necessary for hardware acceptance.
Do not publish a release automatically.
