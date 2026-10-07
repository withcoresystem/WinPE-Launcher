# Third-party notices — WinPE Launcher

This project bundles the following third-party components as embedded resources in
`Launcher.exe`.

## AntdUI
- Library: `lib/AntdUI.dll` (embedded as a resource and loaded via `AssemblyResolve` in `Program.cs`)
- Author: AntdUI project (https://github.com/AntdUI/AntdUI)
- License: Apache License 2.0
- Full text: https://www.apache.org/licenses/LICENSE-2.0

## Inter
- Files: `src/Fonts/Inter-Regular.ttf`, `src/Fonts/Inter-Bold.ttf`
- Copyright: The Inter Project Authors (https://github.com/rsms/inter)
- License: SIL Open Font License 1.1 (OFL-1.1)
- Full text: https://scripts.sil.org/OFL

## Hack
- File: `src/Fonts/Hack-Regular.ttf`
- Copyright: 2018 Source Foundry Authors (https://github.com/source-foundry/Hack)
- License: MIT License; portions from the DejaVu project (public domain) and
  Bitstream Vera Sans Mono (Bitstream Vera License, reserved font names
  "Bitstream" and "Vera")
- Full text: `src/Fonts/Hack-LICENSE.md`

The AntdUI library and the Inter/Hack font files are redistributed unmodified under
their respective licenses. No reserved font name is claimed or modified.
