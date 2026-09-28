using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class CameraSmoke
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Get<T>(View v, string name) => (T)typeof(View).GetField(name, Flags).GetValue(v);
    static void Set(View v, string name, object value) => typeof(View).GetField(name, Flags).SetValue(v, value);
    static void Call(View v, string name, params object[] args) => typeof(View).GetMethod(name, Flags).Invoke(v, args);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void ResetRotation(View v, float yaw = 100f)
    {
        Set(v, "m_CameraRotation", new Vector2(30f, yaw));
        Set(v, "m_CameraRotationSmoothed", new Vector2(30f, yaw));
        Call(v, "ApplyRotation", 1f / 60f);
    }

    public static object Main()
    {
        Check(UnityEditor.EditorApplication.isPlaying, "Run in Main scene Play Mode");
        Check(Application.isFocused, "Focus the Game view before running");
        View v = UnityEngine.Object.FindAnyObjectByType<View>();
        Check(!Get<bool>(v, "m_UseOriginalCamera"), "Improved must be the default");
        Check(Quaternion.Angle(v.transform.rotation, Quaternion.Euler(Get<Vector2>(v, "m_CameraRotation").x, Get<Vector2>(v, "m_CameraRotation").y, 0f)) < 0.01f, "Startup rotation still drifting");
        Check(QualitySettings.vSyncCount == 1 && Application.targetFrameRate == -1, "Improved mode still caps Editor at 35 FPS");
        var target = Get<Transform>(v, "m_Target");
        var pivot = Get<Transform>(v, "m_CameraPivot");
        var camera = Camera.main;
        var savedPosition = v.transform.position;
        var savedRotation = v.transform.rotation;
        var savedPivot = pivot.localPosition;
        var savedZoom = Get<float>(v, "m_Zoom");
        var pad = InputSystem.AddDevice<Gamepad>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        GameObject testTarget = null, trigger = null, wall = null;
        var fpsResults = new List<object>();
        try
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = Vector2.up * 120f });
            InputSystem.Update();
            Call(v, "LateUpdate");
            Check(Mathf.Abs(Get<float>(v, "m_Zoom") - (savedZoom - 0.75f)) < 0.001f, "Wheel should change requested distance by 0.75m");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = Vector2.right * 1000f });
            InputSystem.Update();
            float releasedYaw = v.transform.eulerAngles.y;
            Call(v, "LateUpdate");
            Check(Cursor.lockState == CursorLockMode.None && Mathf.Abs(Mathf.DeltaAngle(releasedYaw, v.transform.eulerAngles.y)) < 0.001f, "Esc must release cursor and stop mouse look");
            float releasedZoom = Get<float>(v, "m_Zoom");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = Vector2.up * 120f });
            InputSystem.Update();
            Call(v, "LateUpdate");
            Check(Get<float>(v, "m_Zoom") == releasedZoom, "Released mouse still changes zoom");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            InputSystem.Update();
            Call(v, "LateUpdate");
            Check(Cursor.lockState == CursorLockMode.Locked, "Click must recapture cursor");
            foreach (Key key in new[] { Key.F1, Key.F2 })
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                Call(v, "LateUpdate");
                Check(Get<bool>(v, "m_UseOriginalCamera") == (key == Key.F1), "F1/F2 did not switch mode");
                Check(Application.targetFrameRate == (key == Key.F1 ? 35 : -1), "Mode frame pacing did not switch");
            }
            v.enabled = false;
            Get<InputActionReference>(v, "m_LookAction").action.Enable();
            Get<InputActionReference>(v, "m_ZoomAction").action.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            foreach (int fps in new[] { 30, 60, 144 })
            {
                ResetRotation(v);
                InputSystem.QueueStateEvent(pad, new GamepadState());
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100f, 20f) });
                InputSystem.Update();
                Call(v, "HandleInput", 1f / fps);
                Call(v, "ApplyRotation", 1f / fps);
                Check(Mathf.Abs(Mathf.DeltaAngle(100f, v.transform.eulerAngles.y) - 15f) < 0.001f, "Mouse response depends on FPS or has rotation lag");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                Call(v, "HandleInput", 1f / fps);
                Call(v, "ApplyRotation", 1f / fps);
                Check(Mathf.Abs(Mathf.DeltaAngle(115f, v.transform.eulerAngles.y)) < 0.001f, "Mouse stop leaves residual rotation");
                ResetRotation(v);
                Set(v, "m_Zoom", 10f);
                InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.right }.WithButton(GamepadButton.RightShoulder));
                InputSystem.Update();
                for (int frame = 0; frame < fps; frame++) Call(v, "HandleInput", 1f / fps);
                Call(v, "ApplyRotation", 1f / fps);
                float yaw = Get<Vector2>(v, "m_CameraRotation").y;
                float zoom = Get<float>(v, "m_Zoom");
                Check(Mathf.Abs(Mathf.DeltaAngle(280f, yaw)) < 0.01f, "Stick speed is not 180 degrees/second at " + fps + " FPS");
                Check(Mathf.Abs(zoom - 7.5f) < 0.001f, "Held zoom depends on FPS at " + fps + " FPS");
                fpsResults.Add(new { fps, mouseYaw = 15f, stickYawAfter1s = yaw, heldZoomAfter1s = zoom });
            }
            InputSystem.QueueStateEvent(pad, new GamepadState());
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = Vector2.up * 10000f });
            InputSystem.Update();
            Call(v, "HandleInput", 1f / 60f);
            Check(Get<Vector2>(v, "m_CameraRotation").x == 0f, "Minimum pitch clamp failed");
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = Vector2.down * 10000f });
            InputSystem.Update();
            Call(v, "HandleInput", 1f / 60f);
            Check(Get<Vector2>(v, "m_CameraRotation").x == 80f, "Maximum pitch clamp failed");
            Call(v, "SetCameraMode", true);
            ResetRotation(v);
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = Vector2.right * 100f });
            InputSystem.Update();
            Call(v, "HandleInput", 1f / 60f);
            Call(v, "ApplyRotation", 1f / 60f);
            float originalFirstFrame = Mathf.DeltaAngle(100f, v.transform.eulerAngles.y);
            Check(Mathf.Abs(originalFirstFrame - 5f) < 0.001f, "Original demo no longer retains rotation damping");
            Call(v, "SetCameraMode", false);
            var origin = new Vector3(200f, 100f, 200f);
            testTarget = new GameObject("CameraSmoke target");
            testTarget.transform.position = origin;
            Set(v, "m_Target", testTarget.transform);
            v.transform.SetPositionAndRotation(origin, Quaternion.identity);
            pivot.localPosition = Vector3.back * 5f;
            Set(v, "m_CollisionDistance", 5f);
            var self = GameObject.CreatePrimitive(PrimitiveType.Cube);
            self.transform.SetParent(testTarget.transform);
            self.transform.localPosition = Vector3.back;
            trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trigger.transform.position = origin + Vector3.back * 2f;
            trigger.GetComponent<Collider>().isTrigger = true;
            Physics.SyncTransforms();
            Call(v, "ApplyCameraPosition", 1f / 60f);
            Check(Mathf.Abs(Vector3.Distance(origin, camera.transform.position) - 5f) < 0.001f, "Player or trigger blocks camera");
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = origin + Vector3.back * 3f;
            wall.transform.localScale = new Vector3(4f, 4f, 0.1f);
            Physics.SyncTransforms();
            Call(v, "ApplyCameraPosition", 1f / 144f);
            float blocked = Vector3.Distance(origin, camera.transform.position);
            Check(blocked > 1.5f && blocked < 2.9f, "Wall did not immediately pull camera forward");
            wall.transform.position = origin + Vector3.back;
            Physics.SyncTransforms();
            Call(v, "ApplyCameraPosition", 1f / 144f);
            float closeDistance = Vector3.Distance(origin, camera.transform.position);
            var renderers = Get<Renderer[]>(v, "m_PlayerRenderers");
            Check(closeDistance < 1.5f && Get<bool>(v, "m_PlayerHidden"), "Close obstruction still shows the inside of the player");
            Check(renderers.Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).All(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly), "Close player should retain only shadows");
            wall.SetActive(false);
            Physics.SyncTransforms();
            Call(v, "ApplyCameraPosition", 1f / 60f);
            float recovering = Vector3.Distance(origin, camera.transform.position);
            Check(recovering > closeDistance && recovering < 5f, "Recovery snaps instead of easing outward");
            for (int frame = 0; frame < 120; frame++) Call(v, "ApplyCameraPosition", 1f / 120f);
            Check(Vector3.Distance(origin, camera.transform.position) > 4.99f, "Camera never recovers full distance");
            Check(!Get<bool>(v, "m_PlayerHidden"), "Player did not reappear after camera recovery");
            return new { passed = true, input = fpsResults, originalFirstFrameYaw = originalFirstFrame, blockedDistance = blocked, closeDistance, nearPlayerClipping = "passed", firstRecoveryDistance = recovering, startupAndHotkeysAndPitchAndCollision = "passed" };
        }
        finally
        {
            if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
            if (trigger != null) UnityEngine.Object.DestroyImmediate(trigger);
            if (testTarget != null) UnityEngine.Object.DestroyImmediate(testTarget);
            Set(v, "m_Target", target);
            v.transform.SetPositionAndRotation(savedPosition, savedRotation);
            pivot.localPosition = savedPivot;
            Set(v, "m_Zoom", savedZoom);
            Set(v, "m_CollisionDistance", savedZoom);
            Set(v, "m_CameraRotation", new Vector2(savedRotation.eulerAngles.x, savedRotation.eulerAngles.y));
            Set(v, "m_CameraRotationSmoothed", Get<Vector2>(v, "m_CameraRotation"));
            Set(v, "m_UseOriginalCamera", false);
            InputSystem.RemoveDevice(pad);
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keyboard);
            Call(v, "ApplyCameraPosition", 0f);
            v.enabled = true;
            Call(v, "SetFrameRate");
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
