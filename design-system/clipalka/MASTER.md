# CLIPALKA — Signal

Approved on 6 November 2026: charcoal desktop UI with a violet accent. The user
approved the Signal mockups, then explicitly replaced acid lime with violet.
This supersedes the dark teal Dota2ProTracker direction and earlier Studio design.
Use functional C# / WPF controls under .NET 8.

## Product structure

- Compact 48-DIP title bar, cut-frame brand mark, three text tabs with an active
  underline: Capture, Clips, Settings. Native window actions; no sidebar.
- Capture opens with a bold heading and an independent, real replay-buffer status.
  Recording changes the heading to a recording message, with a red Stop control
  and elapsed timer. Replay stays independently available when actually running.
- The dominant preview is explicitly the last saved clip. Centered playback,
  actual clip title, saved date and duration. Show the first-clip empty state
  before a clip exists. Never present a saved frame as a live capture feed.
- Action order: Save moment, Record / Stop, Microphone. Wide windows place actual
  keyboard hints on the right inside outlined chips. Under 1400 DIPs, hints move
  below labels without scaling down the whole interface. The microphone action
  includes a real 16-segment violet level meter, dimmed for mute / unavailable
  input. Read the applied microphone endpoint and gain, never simulated motion.
  A shared WASAPI monitoring client runs only while Capture is visible; samples
  are discarded, with no playback or additional audio files.
- A quiet utility row links to source, configured FPS, audio and clips folder.
- Three recent clips, direct thumbnails and ellipsized titles with tooltips.
  Gallery uses the same surfaces and offers playback, search and reveal-in-folder.
- Settings retain Video & files, Audio, Keyboard & controller and explicit Save.
  Preserve learning, current input, saved bindings and blocked-command reasons.
- Quiet status footer with an explicit replay on/off action.
- Tray menu uses the same charcoal canvas, violet selection, stroke icons and
  compact typography. Show real buffer / recording state, applied keyboard hints,
  a Record / Stop label and disabled unavailable commands. Preserve native menu
  dismissal, keyboard navigation and the system high-contrast renderer.

## Visual system

- Canvas #111213, panel #1B1D1E, input #202225, border #3C3E42.
- Primary replay #9566F5, secondary accent #8952DE, hover #B58AFF,
  focus #C8ADFF, ready / enabled #AF80FA. Recording #FF514F on #351D20.
- Text #F5F5F6, secondary #BABBC2, tertiary #94959F.
- Segoe UI Variable Text / Segoe UI; Consolas for keyboard hints and timers.
  Capture heading 44 (36 compact), actions 19 (16 narrow), body 14-16.
- Small 4-DIP corners, fine dividers, neutral surfaces. No ornamental glow,
  surface gradients or nested dashboard cards. Image shading is functional.
- 4/8 spacing rhythm, 16-DIP control gaps, 28-DIP page gutters.
- Existing stroke icons plus the simple cut-frame mark. No font-glyph icons.
- Hover covers the complete control. Padding belongs on ContentPresenter,
  not the template root. Keyboard focus stays visible; tab order follows visuals.

## Functional and verification rules

Normal startup uses real clips and applied settings. Only --render-ui uses the
existing Assets/studio-demo.png and fixture devices. Do not claim a full 30-second
buffer from a boolean running flag; production displays buffer active / off.
Preserve capture selection, native PCM continuity, controller backends,
notifications, installer and user data behavior. Do not publish automatically.

Render real WPF at 1585x992, 1280x800, 1180x760, 1024x768 and 150% DPI.
Cover gallery, settings, empty, recording, error, muted, native device popup and
keyboard focus. Verify control boundaries, compact shortcut baselines,
left-to-right tab order and full-control hover coverage. Capture and recent clips
must fit the default viewport. Keep core tests and native-load verification.
Preserve the existing replay-export, controller-service and installer checks;
run the full release checks before publishing a new version.
Physical Sonar and Bluetooth acceptance remain separate from UI verification.
