using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SwapCharacter
{
    const string Folder = "Assets/NewCharacter/";
    const string ModelPath = Folder + "characterMedium.fbx";
    const string ReadyPath = Folder + "ZombieCharacter.prefab";
    const string PlayerPath = "Assets/Prefabs/Player.prefab";
    const string ControllerPath = "Assets/Art/Animation/CharacterAnimatorController.controller";

    [MenuItem("Tools/Character Workshop/1 Prepare Zombie Prefab")]
    public static void Prepare()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Stop Play Mode before preparing assets.");

        var model = Require<GameObject>(ModelPath);
        var texture = Require<Texture2D>(Folder + "zombieA.png");
        var controller = Require<AnimatorController>(ControllerPath);
        var locomotionState = controller.layers[0].stateMachine.states.Single(s => s.state.name == "Locomotion").state;
        var locomotion = locomotionState.motion as BlendTree;
        var jump = controller.layers[0].stateMachine.states.Single(s => s.state.name == "Jump").state;
        var takeOff = locomotionState.transitions.Single(t => t.destinationState == jump);
        var land = jump.transitions.Single(t => t.destinationState == locomotionState);
        if (locomotion == null || locomotion.children.Length != 3)
            throw new InvalidOperationException("Expected the starter's three-motion Locomotion blend tree.");
        foreach (string name in new[] { "Idle", "Run", "Jump" })
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + name.ToLowerInvariant() + ".fbx");
            if (importer == null || !importer.defaultClipAnimations.Any(c => c.name == "Root|" + name))
                throw new InvalidOperationException("Missing source animation: " + name);
        }
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("This workshop requires URP Lit.");

        Configure(ModelPath);
        foreach (string name in new[] { "Idle", "Run", "Jump" })
            Configure(Folder + name.ToLowerInvariant() + ".fbx", name);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().Single();
        if (!avatar.isValid || !avatar.isHuman)
            throw new InvalidOperationException("Configure a valid Humanoid Avatar before creating the prefab.");

        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "ZombieSkin.mat");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, Folder + "ZombieSkin.mat");
        }
        material.shader = shader;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);

        var children = locomotion.children;
        children[0].motion = Clip("Idle");
        children[1].motion = children[2].motion = Clip("Run");
        locomotion.children = children;
        jump.motion = Clip("Jump");
        takeOff.hasExitTime = land.hasExitTime = false;
        EditorUtility.SetDirty(takeOff);
        EditorUtility.SetDirty(land);
        EditorUtility.SetDirty(locomotion);
        EditorUtility.SetDirty(jump);
        EditorUtility.SetDirty(controller);

        var visual = new GameObject("ZombieCharacter");
        try
        {
            model = Require<GameObject>(ModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, visual.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            instance.transform.localScale = Vector3.one;
            var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            if (PrefabUtility.SaveAsPrefabAsset(visual, ReadyPath) == null)
                throw new InvalidOperationException("Could not save the prepared character prefab.");
        }
        finally { UnityEngine.Object.DestroyImmediate(visual); }
        AssetDatabase.SaveAssets();
        Selection.activeObject = Require<GameObject>(ReadyPath);
        Debug.Log("Prepared ZombieCharacter: import scale 0.28, +Z facing, Humanoid, Idle/Run/Jump, URP material.");
    }

    static void Configure(string path, string animation = null)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.globalScale = 0.28f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true;
        importer.removeConstantScaleCurves = true;
        // Rebuild this pack's automatic Avatar mapping after changing units or axes.
        var human = importer.humanDescription;
        human.human = Array.Empty<HumanBone>();
        human.skeleton = Array.Empty<SkeletonBone>();
        importer.humanDescription = human;
        if (animation != null)
        {
            var clip = importer.defaultClipAnimations.Single(c => c.name == "Root|" + animation);
            clip.loopTime = animation != "Jump";
            clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip };
        }
        importer.SaveAndReimport();
    }

    static AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath(Folder + name.ToLowerInvariant() + ".fbx")
        .OfType<AnimationClip>().Single(c => c.name == "Root|" + name);

    static T Require<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);

    [MenuItem("Tools/Character Workshop/2 Apply Prepared Zombie To Player")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Stop Play Mode before editing Player.");
        var ready = Require<GameObject>(ReadyPath);
        var preparedAnimator = ready.GetComponentInChildren<Animator>();
        if (preparedAnimator == null || preparedAnimator.runtimeAnimatorController == null ||
            preparedAnimator.avatar == null || !preparedAnimator.avatar.isValid || !preparedAnimator.avatar.isHuman)
            throw new InvalidOperationException("Prepare a valid animated character first.");
        var player = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var serialized = new SerializedObject(player.GetComponent<Player>());
            var model = serialized.FindProperty("m_Model");
            var animator = serialized.FindProperty("m_Animator");
            var previous = (Transform)model.objectReferenceValue;
            if (previous == null || previous.parent != player.transform)
                throw new InvalidOperationException("Player Model must refer to its visual child.");
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(ready, player.transform);
            replacement.name = "character";
            replacement.transform.SetSiblingIndex(previous.GetSiblingIndex());
            model.objectReferenceValue = replacement.transform;
            animator.objectReferenceValue = replacement.GetComponentInChildren<Animator>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.DestroyImmediate(previous.gameObject);
            if (PrefabUtility.SaveAsPrefabAsset(player, PlayerPath) == null)
                throw new InvalidOperationException("Could not save Player.");
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        Debug.Log("Player now uses the prepared prefab; its movement and collider remain on Player.");
    }
}
