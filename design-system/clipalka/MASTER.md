# CLIPALKA — монтажный стол

Use the graphite canvas (#100F16), dark panels (#201E2A) and violet primary action (#BA98FF). Use Georgia for page headings and Segoe UI for interface text. The app mark is «Дубль», two overlapping frames with a play cutout. Assets/clipalka-icon.png supplies the navigation mark and Assets/clipalka.ico supplies Windows icons. The first-clip artwork lives in Themes/Editions.xaml.

## Capture and library

Place the three destinations in the left rail. Keep the latest playable clip beside the three recent clips. Put save, record and microphone controls directly below the preview, with applied shortcuts and the live input level. The next row links to the capture source, audio device and clips directory. Separate each button from its neighbours at rest and on hover.

Use real thumbnails and timestamps from the selected folder. Show the illustrated empty state before the first clip exists. Reserve demo clips for the render harness. Show the recording timer and stop action during capture, and the actual buffer status beside the heading.

## Settings and windows

Keep the settings categories and Save action above the scrollable form. Display the selected device, controller learning state, keyboard focus and validation errors. Minimize to the taskbar, hide to the tray on close, and retain the system high-contrast tray renderer.
## Verification

Use --render-ui to verify minimum, compact, wide and high-DPI layouts,
keyboard traversal, control bounds, hover and the actual device popup.
Verify the native capture module when packaging. Follow the Windows test
checklist for release and hardware acceptance.
