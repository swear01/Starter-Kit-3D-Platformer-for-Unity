using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class CharacterSmoke
{
    static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Get<T>(Player player, string name) => (T)typeof(Player).GetField(name, Fields).GetValue(player);
    static void Set(Player player, string name, object value) => typeof(Player).GetField(name, Fields).SetValue(player, value);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Tick(Player player, Animator animator, Gamepad pad, GamepadState state, int count = 1)
    {
        count = count == 1 ? 1 : Mathf.CeilToInt(count / (60f * Time.deltaTime));
        for (int i = 0; i < count; i++)
        {
            InputSystem.QueueStateEvent(pad, state);
            InputSystem.Update();
            typeof(Player).GetMethod("Update", Fields).Invoke(player, null);
            animator.Update(Time.deltaTime);
        }
    }
    static float Height(Animator animator) => animator.GetBoneTransform(HumanBodyBones.Head).position.y -
        (animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y + animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y) / 2f;

    public static object Main()
    {
        Check(EditorApplication.isPlaying && Application.isFocused, "Run in Main Play Mode with Game view focused.");
        Check(Time.deltaTime > 0f && Time.deltaTime < 0.1f, "Expected a normal Play Mode timestep.");
        var live = UnityEngine.Object.FindAnyObjectByType<Player>();
        Check(live != null && live.enabled, "Expected the active Main Player.");
        var ready = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NewCharacter/ZombieCharacter.prefab");
        Check(ready != null && ready.transform.localScale == Vector3.one && ready.transform.localRotation == Quaternion.identity,
            "Prepared visual root must be neutral.");
        var savedAnimator = ready.GetComponentInChildren<Animator>();
        Check(savedAnimator.avatar.isHuman && savedAnimator.avatar.isValid && savedAnimator.humanScale < 2f,
            "Avatar is invalid or retains the old FBX scale.");
        Check(!savedAnimator.applyRootMotion, "CharacterController must own movement.");
        Check(savedAnimator.runtimeAnimatorController.animationClips.All(c => !c.name.Contains("Targeting Pose")), "Controller uses a static pose.");
        foreach (string name in new[] { "idle", "run", "jump" })
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/NewCharacter/" + name + ".fbx");
            Check(importer.clipAnimations.Length == 1 && importer.clipAnimations[0].name == "Root|" + char.ToUpperInvariant(name[0]) + name.Substring(1),
                "Wrong imported clip: " + name);
            Check(importer.clipAnimations[0].loopTime == (name != "jump"), "Wrong loop setting: " + name);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        var prefabPlayer = prefab.GetComponent<Player>();
        Check(Get<Transform>(prefabPlayer, "m_Model").GetComponentInChildren<Animator>() == Get<Animator>(prefabPlayer, "m_Animator"), "Player references old visuals.");
        var liveModel = Get<Transform>(live, "m_Model");
        var liveAnimator = Get<Animator>(live, "m_Animator");
        Check(Height(liveAnimator) > 0.5f && Height(liveAnimator) < 1.5f, "Live animation changes character height.");
        var initialLiveScale = liveModel.localScale;
        var moveRef = Get<InputActionReference>(live, "m_MoveAction");
        var jumpRef = Get<InputActionReference>(live, "m_JumpAction");
        bool moveEnabled = moveRef.action.enabled, jumpEnabled = jumpRef.action.enabled;
        GameObject holder = null, ground = null;
        Gamepad pad = null;
        InputAction move = null, jump = null;
        InputActionAsset testActions = null;
        InputActionMap testMap = null;
        InputActionReference testMove = null, testJump = null;
        try
        {
            live.enabled = false;
            pad = InputSystem.AddDevice<Gamepad>();
            testActions = ScriptableObject.CreateInstance<InputActionAsset>();
            testMap = new InputActionMap("CharacterSmoke");
            testActions.AddActionMap(testMap);
            move = testMap.AddAction("TestMove", InputActionType.Value, "<Gamepad>/leftStick");
            jump = testMap.AddAction("TestJump", InputActionType.Button, "<Gamepad>/buttonSouth");
            testMap.devices = new InputDevice[] { pad };
            testMove = InputActionReference.Create(move);
            testJump = InputActionReference.Create(jump);
            holder = new GameObject("CharacterSmokePlayer");
            holder.SetActive(false);
            var instance = UnityEngine.Object.Instantiate(prefab, holder.transform);
            instance.transform.position = new Vector3(500f, 2f, 500f);
            var player = instance.GetComponent<Player>();
            Set(player, "m_MoveAction", testMove);
            Set(player, "m_JumpAction", testJump);
            Set(player, "m_RunTrail", null);
            Set(player, "m_FootstepsAudio", null);
            Set(player, "m_JumpAudio", null);
            Set(player, "m_LandAudio", null);
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "CharacterSmokeGround";
            ground.transform.position = new Vector3(500f, -0.5f, 500f);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            Physics.SyncTransforms();
            holder.SetActive(true);
            var animator = Get<Animator>(player, "m_Animator");
            var model = Get<Transform>(player, "m_Model");
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Tick(player, animator, pad, new GamepadState(), 100);
            Check(instance.GetComponent<CharacterController>().isGrounded, "Prepared character did not land on the test platform.");
            Tick(player, animator, pad, new GamepadState(), 8);
            Check(Height(animator) > 0.5f && Height(animator) < 1.5f, "Idle warps the skeleton scale.");
            var idleArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localRotation;
            var start = instance.transform.position;
            Tick(player, animator, pad, new GamepadState { leftStick = Vector2.up }, 18);
            var travel = instance.transform.position - start;
            travel.y = 0;
            Check(travel.magnitude > 0.1f && Vector3.Dot(travel.normalized, instance.transform.forward) > 0.9f, "Movement and player facing disagree.");
            var shoulders = animator.GetBoneTransform(HumanBodyBones.RightShoulder).position - animator.GetBoneTransform(HumanBodyBones.LeftShoulder).position;
            var bodyForward = Vector3.Cross(shoulders, Vector3.up).normalized;
            Check(Vector3.Dot(bodyForward, instance.transform.forward) > 0.8f,
                "Animated body faces against movement: " + Vector3.Dot(bodyForward, instance.transform.forward));
            Check(Quaternion.Angle(idleArm, animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localRotation) > 1f, "Run does not animate the skeleton.");
            Check(Height(animator) > 0.4f && Height(animator) < 1.5f, "Run warps the skeleton scale.");
            Tick(player, animator, pad, new GamepadState(), 2);
            Tick(player, animator, pad, new GamepadState().WithButton(GamepadButton.South));
            Check(Get<int>(player, "m_JumpsRemaining") == 1 && !instance.GetComponent<CharacterController>().isGrounded, "First jump failed.");
            Tick(player, animator, pad, new GamepadState(), 2);
            Tick(player, animator, pad, new GamepadState().WithButton(GamepadButton.South));
            Check(Get<int>(player, "m_JumpsRemaining") == 0, "Double jump failed.");
            Tick(player, animator, pad, new GamepadState(), 14);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Jump") ||
                (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Jump")), "Animator did not enter Jump.");
            Tick(player, animator, pad, new GamepadState(), 100);
            Check(instance.GetComponent<CharacterController>().isGrounded, "Jump did not land.");
            Tick(player, animator, pad, new GamepadState(), 50);
            Check(Vector3.Distance(model.localScale, Vector3.one) < 0.005f, "Landing does not restore the prepared size.");
            Check(Height(animator) > 0.5f && Height(animator) < 1.5f, "Landing warps the skeleton scale.");
            model.localScale = Vector3.one * 0.65f;
            model.localRotation = Quaternion.Euler(0f, 15f, 0f);
            typeof(Player).GetMethod("Awake", Fields).Invoke(player, null);
            Check(Quaternion.Angle(model.localRotation, Quaternion.Euler(0f, 15f, 0f)) < 0.01f, "Awake overwrites authored facing.");
            typeof(Player).GetMethod("Jump", Fields).Invoke(player, null);
            Check(Vector3.Distance(model.localScale, Vector3.Scale(Vector3.one * 0.65f, new Vector3(0.5f, 1.5f, 0.5f))) < 0.001f,
                "Jump replaces the authored baseline scale.");
            Tick(player, animator, pad, new GamepadState(), 100);
            Tick(player, animator, pad, new GamepadState(), 50);
            Check(Vector3.Distance(model.localScale, Vector3.one * 0.65f) < 0.005f, "Effects reset a non-unit visual to size 1.");
            return new { passed = true, ready = ready.name, avatarScale = savedAnimator.humanScale, liveHeight = Height(liveAnimator), moved = travel.magnitude, facing = Vector3.Dot(travel.normalized, instance.transform.forward), checks = "import, refs, idle/run/jump, model facing, movement, double jump, scale recovery, authored rotation/scale" };
        }
        finally
        {
            if (holder != null) UnityEngine.Object.DestroyImmediate(holder);
            if (ground != null) UnityEngine.Object.DestroyImmediate(ground);
            testMap?.Dispose();
            if (testActions != null) UnityEngine.Object.DestroyImmediate(testActions);
            if (testMove != null) UnityEngine.Object.DestroyImmediate(testMove);
            if (testJump != null) UnityEngine.Object.DestroyImmediate(testJump);
            if (pad != null) InputSystem.RemoveDevice(pad);
            live.enabled = true;
            if (!moveEnabled) moveRef.action.Disable();
            if (!jumpEnabled) jumpRef.action.Disable();
            Check(Vector3.Distance(liveModel.localScale, initialLiveScale) < 0.005f, "Test changed live character size.");
        }
    }
}
