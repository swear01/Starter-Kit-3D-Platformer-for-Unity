<p align="center"><img src="Assets/.Documentation/icon.png"/></p>

# Starter-Kit-3D-Platformer for Unity

> [!WARNING]
> This project is an unofficial **Unity port** of the original [Starter Kit 3D Platformer](https://github.com/KenneyNL/Starter-Kit-3D-Platformer) by [Kenney](https://kenney.nl). It is not affiliated with or endorsed by Kenney.

This repository includes a basic template for a 3D platformer game in Unity 6.4. Includes features like;

- Character controller (with double jump)
- Collectable coins and falling platforms
- Camera controls (rotate, zoom)
- Gamepad support
- Sprites and 3D Models _(CC0 licensed)_
- Sound effects _(CC0 licensed)_

### Screenshot

<p align="center"><img src="Assets/.Documentation/screenshot.png"/></p>

### Camera workshop variant

The improved camera is enabled by default. Mouse look responds immediately; gamepad look uses degrees per second. Follow damping is shorter, the camera starts in its intended pose, and solid obstacles pull it closer to the player. Coins and the player's own colliders do not block the camera. If an obstacle forces the camera inside the 1.5m player visibility distance, the model is temporarily rendered as shadows only and reappears after the camera recovers.

- Move with WASD or the left stick; jump with Space or the south gamepad button.
- Rotate with the mouse or right stick. The mouse is captured during play; Escape releases it, and clicking the Game view captures it again.
- Zoom with the wheel, Page Up/Down, or gamepad shoulders. Held buttons now zoom continuously.
- Hidden demo shortcuts: F1 selects the original camera behavior, F2 restores the improved camera. On macOS, use Fn with F1/F2 if those keys control brightness. There is no camera status text on the game screen.
- Select the Camera object to tune `Mouse Sensitivity` (horizontal/vertical degrees per pixel), `Gamepad Sensitivity` (degrees per second), `Comfort Follow Smooth`, `Pivot Height`, `Wheel Zoom Step`, `Collision Mask`, and `Player Visible Distance` in the Inspector.

The Player aligns its visual model to the controller's local +Z movement direction at startup, fixing the reversed Humanoid model in the workshop project. Existing model scale is preserved.

`Look` now supplies raw pointer/stick values. Original mode restores the old scaling and damping for comparison. In the Editor, original mode requests 35 FPS with VSync off; improved mode enables VSync and removes that cap. Actual refresh rate depends on the display and Editor.

With the optional Unity Pipeline and Unity CLI already available, open Main in Play Mode, focus the Game view, and run the regression check (no test framework or new project dependency):

```sh
unity command --project-path /path/to/project run_script --file Tests/CameraSmoke.cs --entry CameraSmoke.Main
```

The check covers raw mouse input and stop response, stick/held-zoom behavior at simulated 30/60/144 FPS, pitch limits, hidden shortcuts, cursor release/capture, obstacle avoidance, trigger/self filtering, close-player visibility, and recovery. It creates temporary physics objects in Play Mode and removes them afterward; it does not save the scene.

References: [Pointer input](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/api/UnityEngine.InputSystem.Pointer.html), [sphere sweep behavior](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Physics.SphereCastAll.html).

### License

MIT License

Copyright (c) 2026 Pomdap

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

Assets included in this package (2D sprites, 3D models and sound effects) are [CC0 licensed](https://creativecommons.org/publicdomain/zero/1.0/)
