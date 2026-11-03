# CLIPALKA — selected Studio concept

The user selected **Studio**, the last of three generated concepts. The approved
composition is saved in [studio-reference.png](studio-reference.png).
Implement the composition as functional C# / WPF controls under .NET 8.

## Product structure

- Slim 48-DIP Windows title bar. CP wordmark and CLIPALKA on the left.
- Centered pill navigation: Capture, My clips, Settings. No sidebar.
- Capture: centered headline and real replay/recording state; a full-width
  preview of the last saved clip; three rectangular action cards underneath.
- Actions: start/stop recording, save 30 seconds, microphone toggle. Their actual
  configured English shortcut hints live inside the controls.
- Three quick links: capture source settings, audio settings, clips folder.
- Three wide recent clips, with real thumbnails, filename, saved date and duration.
- Settings has Video & files, Audio, Keyboard & controller sections. Forms and
  editable shortcuts appear here, with an explicit Save action.
- A quiet footer retains capture status, detailed errors/status and replay toggle.

## Visual system

- Canvas: restrained graphite gradient #22232D → #181A22 → #20212A.
- Action surfaces: #31333F → #262832, 20-DIP corners, thin #4C4E5B border.
- Primary replay action and selected top tab: #8B46E8 → #6536D2.
- Text #F5F5FA, secondary #B1B2C4, focus #C2AAFF, ready #4DE3B1,
  record #FF506C. State always includes text or a glyph.
- Segoe UI Variable Text / Segoe UI. Headline 42 (30–34 compact),
  actions 23 (16–19 compact), metadata 14–16. No whole-window shrinking.
- Hero 300 DIPs at 1585×992; adaptive 160–340 at other viewport heights.
- 4/8 spacing rhythm, 16-DIP action/quick-link gaps, 28-DIP page gutters.
- One stroke icon family. Hover keeps the replay action purple.

## Functional details

The preview uses the latest real saved clip in normal startup. Its Open action
launches that file through the Windows default video app. It does not pretend
to be a live capture monitor or an inline video player.

The source name, FPS, mute state and shortcut hints come from the existing
capture service/settings. Preserve the native PCM continuity fix, game capture
selection, Bluetooth input services, notifications and installer behavior.

The thumbnail artwork in [Assets/studio-demo.png](../../src/Clipalka.App/Assets/studio-demo.png)
is used **only** by the explicit --render-ui verification command. No sample clip
is added to the user's gallery or settings. See [ARTWORK.md](ARTWORK.md) for its
generation method and prompt. It must never become a fallback for actual recordings.

## Verification

Render real WPF at 1585×992, 1280×800, 1180×760, 1024×768 and 150% DPI.
Include gallery, video settings, audio settings, controller settings, empty,
recording and error states, real device popup and keyboard focus.
Capture and recent clips fit the default viewport; primary actions stay within
their bounds and shortcut baselines align. Settings sections must all be reachable.
Keep existing core, native audio, export and upgrade/uninstall checks passing.
Do not publish a release automatically.
