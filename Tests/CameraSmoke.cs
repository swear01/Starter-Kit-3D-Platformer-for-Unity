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
    static T Get<T>(View v, string name) => (T)(typeof(View).GetField(name, Flags) ?? throw new Exception($"View.{name} field not found")).GetValue(v);
    static void Set(View v, string name, object value) => (typeof(View).GetField(name, Flags) ?? throw new Exception($"View.{name} field not found")).SetValue(v, value);
    static void Call(View v, string name, params object[] args) => (typeof(View).GetMethod(name, Flags) ?? throw new Exception($"View.{name} method not found")).Invoke(v, args);
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
        Check(v != null, "No View found in scene");
        Check(!Get<bool>(v, "m_UseOriginalCamera"), "Improved must be the default");
        Check(Quaternion.Angle(v.transform.rotation, Quaternion.Euler(Get<Vector2>(v, "m_CameraRotation").x, Get<Vector2>(v, "m_CameraRotation").y, 0f)) < 0.01f, "Startup rotation still drifting");
        Check(QualitySettings.vSyncCount == 1 && Application.targetFrameRate == -1, "Improved mode still caps Editor at 35 FPS");
        var target = Get<Transform>(v, "m_Target");
        var pivot = Get<Transform>(v, "m_CameraPivot");
        var camera = Camera.main;
        Check(camera != null, "No Main Camera found in scene");
        Check(target != null && pivot != null, "View target/pivot missing");
        var savedPosition = v.transform.position;
        var savedRotation = v.transform.rotation;
        var savedPivot = pivot.localPosition;
        var savedCameraPosition = camera.transform.position;
        var savedCameraRotation = camera.transform.rotation;
        var savedCollision = Get<float>(v, "m_CollisionDistance");
        var shadowModes = Get<ShadowCastingMode[]>(v, "m_ShadowModes");
        var savedHidden = Get<bool>(v, "m_PlayerHidden");
        var savedFollow = Get<float>(v, "m_ComfortFollowSmooth");
        var mouseSensitivity = Get<Vector2>(v, "m_MouseSensitivity");
        var stickSensitivity = Get<Vector2>(v, "m_GamepadSensitivity");
        var wheelStep = Get<float>(v, "m_WheelZoomStep");
        var savedZoom = Get<float>(v, "m_Zoom");
        var savedHold = Get<float>(v, "m_CollisionHoldRemaining");
        var savedHeldDistance = Get<float>(v, "m_HeldCollisionDistance");
        var pad = InputSystem.AddDevice<Gamepad>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        GameObject testTarget = null, trigger = null, wall = null;
        var fpsResults = new List<object>();
        var approachResults = new List<object>();
        var orbitResults = new List<object>();
        try
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = Vector2.up * 120f });
            InputSystem.Update();
            Call(v, "LateUpdate");
            Check(Mathf.Abs(Get<float>(v, "m_Zoom") - Mathf.Clamp(savedZoom - wheelStep, Get<float>(v, "m_ZoomMin"), Get<float>(v, "m_ZoomMax"))) < 0.001f, "Wheel should apply configured zoom step");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.Update();
            Call(v, "LateUpdate");
            Set(v, "m_Zoom", 10f);
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = Vector2.up * 120f });
            InputSystem.Update();
            Call(v, "LateUpdate");
            Check(Mathf.Abs(Get<float>(v, "m_Zoom") - (10f - wheelStep)) < 0.001f, "Same-frame device switch mixes held zoom into wheel steps");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.Update();
            Call(v, "LateUpdate");
            Set(v, "m_Zoom", 10f);
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = Vector2.up * 120f });
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.Update();
            Call(v, "LateUpdate");
            Check(Mathf.Abs(Get<float>(v, "m_Zoom") - (10f - wheelStep)) < 0.001f, "Canceled transient wheel event lost its accumulated step");
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
            v.enabled = true;
            Check(Cursor.lockState == CursorLockMode.Locked, "Re-enabling improved camera must capture the cursor");
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
                Check(Mathf.Abs(Mathf.DeltaAngle(100f, v.transform.eulerAngles.y) - 100f * mouseSensitivity.x) < 0.001f, "Mouse response depends on FPS or has rotation lag");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                Call(v, "HandleInput", 1f / fps);
                Call(v, "ApplyRotation", 1f / fps);
                Check(Mathf.Abs(Mathf.DeltaAngle(100f + 100f * mouseSensitivity.x, v.transform.eulerAngles.y)) < 0.001f, "Mouse stop leaves residual rotation");
                ResetRotation(v);
                Set(v, "m_Zoom", 10f);
                InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.right }.WithButton(GamepadButton.RightShoulder));
                InputSystem.Update();
                for (int frame = 0; frame < fps; frame++) Call(v, "HandleInput", 1f / fps);
                Call(v, "ApplyRotation", 1f / fps);
                float yaw = Get<Vector2>(v, "m_CameraRotation").y;
                float zoom = Get<float>(v, "m_Zoom");
                Check(Mathf.Abs(Mathf.DeltaAngle(100f + stickSensitivity.x, yaw)) < 0.01f, "Stick speed differs from configured degrees/second at " + fps + " FPS");
                Check(Mathf.Abs(zoom - Mathf.Clamp(10f - 0.1f * Get<float>(v, "m_ZoomSpeed"), Get<float>(v, "m_ZoomMin"), Get<float>(v, "m_ZoomMax"))) < 0.001f, "Held zoom depends on FPS at " + fps + " FPS");
                fpsResults.Add(new { fps, mouseYaw = 100f * mouseSensitivity.x, stickYawAfter1s = yaw, heldZoomAfter1s = zoom });
            }
            InputSystem.QueueStateEvent(pad, new GamepadState());
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = Vector2.up * 10000f });
            InputSystem.Update();
            Call(v, "HandleInput", 1f / 60f);
            Check(Get<Vector2>(v, "m_CameraRotation").x == Get<float>(v, "m_MinPitch"), "Minimum pitch clamp failed");
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = Vector2.down * 10000f });
            InputSystem.Update();
            Call(v, "HandleInput", 1f / 60f);
            Check(Get<Vector2>(v, "m_CameraRotation").x == Get<float>(v, "m_MaxPitch"), "Maximum pitch clamp failed");
            Call(v, "SetCameraMode", true);
            ResetRotation(v);
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = Vector2.right * 100f });
            InputSystem.Update();
            Call(v, "HandleInput", 1f / 60f);
            Call(v, "ApplyRotation", 1f / 60f);
            float originalFirstFrame = Mathf.DeltaAngle(100f, v.transform.eulerAngles.y);
            Check(Mathf.Abs(originalFirstFrame - 5f * Get<float>(v, "m_RotationSpeedY") * Mathf.Clamp01(Get<float>(v, "m_RotationSmooth") / 60f)) < 0.001f, "Original demo no longer retains rotation damping");
            Call(v, "SetCameraMode", false);
            Set(v, "m_ComfortFollowSmooth", 0f);
            Call(v, "FollowTarget", 1f / 60f);
            Check(Vector3.Distance(v.transform.position, target.position + Vector3.up * Get<float>(v, "m_PivotHeight")) < 0.001f, "Zero follow damping must follow immediately");
            Set(v, "m_ComfortFollowSmooth", savedFollow);
            float nearHeight = camera.nearClipPlane * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float radius = Mathf.Max(Get<float>(v, "m_CameraRadius"), Mathf.Sqrt(nearHeight * nearHeight * (1f + camera.aspect * camera.aspect) + camera.nearClipPlane * camera.nearClipPlane));
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
            var crowdedSelf = new GameObject("CameraSmoke many self colliders");
            crowdedSelf.transform.SetParent(testTarget.transform, false);
            crowdedSelf.transform.localPosition = Vector3.back * 2f;
            for (int i = 0; i < 40; i++) crowdedSelf.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            var buffer = Get<RaycastHit[]>(v, "m_HitBuffer");
            Check(Physics.SphereCastNonAlloc(origin, radius, Vector3.back, buffer, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) == buffer.Length, "Fixture did not saturate hit buffer");
            Call(v, "ApplyCameraPosition", 1f / 144f);
            float blocked = Vector3.Distance(origin, camera.transform.position);
            Check(blocked > 1.5f && blocked < 2.9f, "Wall did not immediately pull camera forward");
            wall.transform.position = origin + Vector3.back;
            Call(v, "ApplyCameraPosition", 1f / 144f);
            float closeDistance = Vector3.Distance(origin, camera.transform.position);
            var renderers = Get<Renderer[]>(v, "m_PlayerRenderers");
            Check(closeDistance < 1.5f && Get<bool>(v, "m_PlayerHidden"), "Close obstruction still shows the inside of the player");
            var bodyRenderers = renderers.Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
            Check(bodyRenderers.Length > 0, "No player body renderer to verify visibility");
            Check(bodyRenderers.All(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly), "Close player should retain only shadows");
            crowdedSelf.SetActive(false);
            wall.transform.position = origin + Vector3.back * 0.45f;
            Call(v, "ApplyCameraPosition", 1f / 60f);
            Check(Vector3.Distance(origin, camera.transform.position) < 0.001f, "A wall overlapping the initial sweep sphere was missed");
            wall.SetActive(false);
            Physics.SyncTransforms();
            Call(v, "ApplyCameraPosition", 1f / 60f);
            float recovering = Vector3.Distance(origin, camera.transform.position);
            Check(recovering >= 0f && recovering <= closeDistance, "Cleared obstacle should hold the near position briefly");
            for (int frame = 0; frame < 360; frame++) Call(v, "ApplyCameraPosition", 1f / 120f);
            Check(Vector3.Distance(origin, camera.transform.position) > 4.99f, "Camera never recovers full distance");
            Check(!Get<bool>(v, "m_PlayerHidden"), "Player did not reappear after camera recovery");
            for (int i = 0; i < renderers.Length; i++)
                Check(renderers[i].shadowCastingMode == shadowModes[i], "Player shadow mode did not recover");
            wall.SetActive(true);
            foreach (int fps in new[] { 30, 60, 144 })
            {
                wall.transform.position = origin + Vector3.back * 6f;
                Set(v, "m_CollisionDistance", 5f);
                Set(v, "m_CollisionHoldRemaining", 0f);
                Call(v, "ApplyCameraPosition", 1f / fps);
                float first = Vector3.Distance(origin, camera.transform.position);
                Check(first < 5f && first > 4.8f, "Look-ahead should begin a smooth pull before physical collision");
                float previous = first, biggestStep = 0f;
                for (int frame = 1; frame <= fps; frame++)
                {
                    float wallDistance = 6f - 3f * frame / fps;
                    wall.transform.position = origin + Vector3.back * wallDistance;
                    Call(v, "ApplyCameraPosition", 1f / fps);
                    float current = Vector3.Distance(origin, camera.transform.position);
                    float safe = Mathf.Min(5f, wallDistance - 0.1f - radius);
                    Check(current <= safe + 0.001f, "Smoothed approach penetrates the wall at " + fps + " FPS");
                    biggestStep = Mathf.Max(biggestStep, Mathf.Abs(current - previous));
                    previous = current;
                }
                Check(biggestStep < 0.15f, "Walking approach still pops at " + fps + " FPS");
                wall.SetActive(false);
                float beforeRelease = Get<float>(v, "m_CollisionDistance");
                for (int frame = 0; frame < Mathf.FloorToInt(fps * 0.1f); frame++) Call(v, "ApplyCameraPosition", 1f / fps);
                Check(Get<float>(v, "m_CollisionDistance") <= beforeRelease + 0.001f, "Obstacle edge flicker immediately pulls camera back out");
                for (int frame = 0; frame < fps * 3; frame++) Call(v, "ApplyCameraPosition", 1f / fps);
                Check(Get<float>(v, "m_CollisionDistance") > 4.99f, "Delayed recovery failed at " + fps + " FPS");
                approachResults.Add(new { fps, firstLookAheadDistance = first, biggestStep, approachEndDistance = previous });
                wall.SetActive(true);
            }
            wall.transform.localScale = new Vector3(0.1f, 4f, 0.3f);
            wall.transform.position = origin + new Vector3(0.9f, 0f, -3f);
            Set(v, "m_CollisionDistance", 5f);
            Set(v, "m_CollisionHoldRemaining", 0f);
            Call(v, "ApplyCameraPosition", 1f / 60f);
            float sideAnticipation = Get<float>(v, "m_CollisionDistance");
            Check(sideAnticipation < 5f && sideAnticipation > 3f, "Side feeler should anticipate an edge without snapping");
            wall.SetActive(false);
            Call(v, "ApplyCameraPosition", 1f / 60f);
            Check(Get<float>(v, "m_CollisionDistance") <= sideAnticipation, "One-frame platform edge causes outward pumping");
            wall.SetActive(true);
            wall.transform.localScale = new Vector3(1f, 4f, 0.3f);
            wall.transform.position = origin + new Vector3(-1.5f, 0f, -3f);
            foreach (int fps in new[] { 30, 60, 144 })
            {
                Set(v, "m_CollisionDistance", 5f);
                Set(v, "m_CollisionHoldRemaining", 0f);
                float previous = 5f, biggestStep = 0f;
                for (int frame = 0; frame <= fps * 2; frame++)
                {
                    v.transform.rotation = Quaternion.Euler(0f, -30f + 30f * frame / fps, 0f);
                    Call(v, "ApplyCameraPosition", 1f / fps);
                    float current = Get<float>(v, "m_CollisionDistance");
                    biggestStep = Mathf.Max(biggestStep, Mathf.Abs(current - previous));
                    previous = current;
                }
                Check(biggestStep < 13f / fps, "Gentle orbit still jumps across the obstacle at " + fps + " FPS");
                orbitResults.Add(new { fps, degreesPerSecond = 30, biggestStep });
            }
            var savedCamera = Get<Camera>(v, "m_Camera");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            v.enabled = true;
            Call(v, "SetPlayerVisibility", false);
            Set(v, "m_Camera", null);
            try
            {
                Call(v, "LateUpdate");
                Check(!v.enabled && !Get<bool>(v, "m_PlayerHidden") && Cursor.lockState == CursorLockMode.None && Cursor.visible, "Missing camera must restore captured cursor and player visibility");
            }
            finally { Set(v, "m_Camera", savedCamera); }
            v.enabled = true;
            UnityEngine.Object.DestroyImmediate(testTarget);
            var missingTargetPosition = v.transform.position;
            Call(v, "LateUpdate");
            Check(!v.enabled && Cursor.lockState == CursorLockMode.None && v.transform.position == missingTargetPosition, "Missing target should leave the camera in place");
            return new { passed = true, missingReferencesCleanupAndInitialOverlap = "passed", input = fpsResults, originalFirstFrameYaw = originalFirstFrame, blockedDistance = blocked, closeDistance, nearPlayerClipping = "passed", bufferSaturationAndSameFrameMovement = "passed", firstRecoveryDistance = recovering, approach = approachResults, orbit = orbitResults, sideAnticipation, startupAndHotkeysAndPitchAndCollision = "passed" };
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
            Set(v, "m_CollisionDistance", savedCollision);
            Set(v, "m_ComfortFollowSmooth", savedFollow);
            Set(v, "m_CollisionHoldRemaining", 0f);
            Set(v, "m_CameraRotation", new Vector2(savedRotation.eulerAngles.x, savedRotation.eulerAngles.y));
            Set(v, "m_CameraRotationSmoothed", Get<Vector2>(v, "m_CameraRotation"));
            Set(v, "m_UseOriginalCamera", false);
            InputSystem.RemoveDevice(pad);
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keyboard);
            camera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
            Call(v, "SetPlayerVisibility", !savedHidden);
            Set(v, "m_CollisionHoldRemaining", savedHold);
            Set(v, "m_HeldCollisionDistance", savedHeldDistance);
            v.enabled = true;
            Call(v, "SetFrameRate");
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
