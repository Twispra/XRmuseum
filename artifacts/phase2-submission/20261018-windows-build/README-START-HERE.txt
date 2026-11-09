XRmuseum - COMP5424 Group 2 - Phase 2
Windows x64 / PC VR / OpenXR

SOURCE AND BUILD
Repository: https://github.com/Twispra/XRmuseum
Source: 6088c736e89d05101e09645c1796556012a6fd5d (main, 18 October 2026)
Unity: 2022.3.62f3c1
Scene: Assets/Museum/Scenes/MuseumStart.unity
Build configuration: Windows x64 Development, Mono scripting backend.
PACKAGE-MANIFEST.json records the complete package contents and SHA-256 hashes.

START
1. Extract the ENTIRE ZIP to a local folder. Do not run the EXE from inside the ZIP.
2. Keep MuseumVR.exe, MuseumVR_Data, UnityPlayer.dll, MonoBleedingEdge and all
   other supplied files/folders together. Unity Editor is not needed to run this build.
3. Connect a compatible PC VR headset and tracked controllers. Start the lab's
   configured VR software and ensure its OpenXR runtime is active and the headset
   is ready (for example, the lab's SteamVR or Meta Quest Link setup).
4. Launch MuseumVR.exe. Put on the headset and follow the entrance instructions.

The initial language is English. Choose English / 简体中文 / 繁體中文 at the
entrance language board or in Settings. The central exhibit initially shows Mark V.

CONTROLS
Use a controller ray or near interaction. Select with the GRIP button.
For central-plinth and left-exhibit buttons, press and release while still
targeting the same button. Moving away cancels the pending button action.
The entrance language board and knowledge hotspots can react on press.

- Move: use the left controller's joystick.
- Rotate the central helmet: target its surface, hold grip and move your hand;
  release to stop. The helmet rotates around a fixed centre.
- Window: select the actual front window to open or close it.
- Section: select Section, then hold and drag the slider along its fixed rail.
  Select Close section to restore the exterior.
- Component information: select a highlighted hotspot.
- Air / Voice: select the corresponding marker to show a demonstration;
  select it again or Close demo to stop. These are interpretive demonstrations.
- Previous era / Next era: switch between the early helmet, Mark V and SuperLite 17B.
- Reset: restore the central exhibit. This does not start a completely new visit.
- Settings / About: select the corresponding buttons on the central plinth.
  Show panel also summons Settings; it becomes Hide panel while a panel is open.
  Release any ongoing manipulation before opening or hiding a panel. If there is
  insufficient clear space, move to a clearer position and summon the panel again.
- Settings: change language, viewing height, sound, motion and hints.
  Reset View restores the starting horizontal viewing position and direction;
  it does not recalibrate physical eye height.
- Left exhibit: use Turn left / Reset / Turn right. Each turn is 30 degrees.
  This is a separate exterior study with its own controls.

EXIT AND RESTART
Close the desktop application window or press Alt+F4. To begin a completely new
visit for another participant, close and relaunch the application.

ENVIRONMENT AND VERIFICATION LIMITS
This package targets Windows PC VR. It is not an Android/standalone Quest build.
The standalone Player does not automatically provide the Editor's keyboard/mouse
XR simulator. A desktop window alone does not verify headset tracking or input.

The build and package are checked locally. Physical headset/controller operation,
stereo rendering, accessibility, comfort, frame rate and the exact PQ604A/B/C
hardware/software environment still require on-site verification.
If the headset is not detected, verify the connection and active OpenXR runtime
before relaunching. The local build process does not change the system VR runtime.

中文启动提示
请先把整个 ZIP 完整解压，再连接电脑 VR 头显和控制器，启动教室已配置的 VR 软件及
对应 OpenXR 运行时，最后打开 MuseumVR.exe。所有附带文件和文件夹必须保留在一起。
使用控制器侧面的 Grip 抓握键选择和操作；可在入口语言板或 Settings 中切换中文。
退出请关闭电脑上的程序窗口或按 Alt+F4。为下一位访客开始全新体验，请退出后重新启动。
本包已进行本地构建与打包检查；PQ604 教室及真实头显的运行情况仍需现场核验。
