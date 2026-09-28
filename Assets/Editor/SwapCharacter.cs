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
    const string ControllerPath = Folder + "ZombieAnimatorController.controller";
    const string TemplatePath = "Assets/Art/Animation/CharacterAnimatorController.controller";
    const float ImportScale = 0.28f;

    [MenuItem("Tools/Character Workshop/1 Prepare Zombie Prefab")]
    public static void Prepare()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Stop Play Mode before preparing assets.");

        var model = Require<GameObject>(ModelPath);
        var texture = Require<Texture2D>(Folder + "zombieA.png");
        Require<AnimatorController>(TemplatePath);
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null && !AssetDatabase.CopyAsset(TemplatePath, ControllerPath))
            throw new InvalidOperationException("Could not copy the starter Animator Controller.");
        var controller = Require<AnimatorController>(ControllerPath);
        if (controller.layers.Length == 0)
            throw new InvalidOperationException("Character Animator Controller has no layers.");
        var locomotionState = controller.layers[0].stateMachine.states.SingleOrDefault(s => s.state.name == "Locomotion").state
            ?? throw new InvalidOperationException("Character Controller needs a Locomotion state.");
        var locomotion = locomotionState.motion as BlendTree;
        var jump = controller.layers[0].stateMachine.states.SingleOrDefault(s => s.state.name == "Jump").state
            ?? throw new InvalidOperationException("Character Controller needs a Jump state.");
        var takeOff = locomotionState.transitions.SingleOrDefault(t => t.destinationState == jump)
            ?? throw new InvalidOperationException("Missing Locomotion to Jump transition.");
        var land = jump.transitions.SingleOrDefault(t => t.destinationState == locomotionState)
            ?? throw new InvalidOperationException("Missing Jump to Locomotion transition.");
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
        var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().SingleOrDefault()
            ?? throw new InvalidOperationException("Missing Avatar in " + ModelPath);
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
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, visual.transform)
                ?? throw new InvalidOperationException("Could not instantiate " + ModelPath);
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
        Debug.Log($"Prepared ZombieCharacter: import scale {ImportScale}, +Z facing, Humanoid, Idle/Run/Jump, URP material.");
    }

    static void Configure(string path, string animation = null)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.globalScale = ImportScale;
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
            var clip = importer.defaultClipAnimations.SingleOrDefault(c => c.name == "Root|" + animation)
                ?? throw new InvalidOperationException("Missing " + animation + " in " + path);
            clip.loopTime = animation != "Jump";
            clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip };
        }
        importer.SaveAndReimport();
    }

    static AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath(Folder + name.ToLowerInvariant() + ".fbx")
        .OfType<AnimationClip>().SingleOrDefault(c => c.name == "Root|" + name)
        ?? throw new InvalidOperationException("Missing imported animation: " + name);

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
        Require<GameObject>(PlayerPath);
        var player = PrefabUtility.LoadPrefabContents(PlayerPath)
            ?? throw new InvalidOperationException("Could not open " + PlayerPath);
        try
        {
            var component = player.GetComponent<Player>()
                ?? throw new InvalidOperationException("Player component is missing from the prefab root.");
            var serialized = new SerializedObject(component);
            var model = serialized.FindProperty("m_Model")
                ?? throw new InvalidOperationException("Player Model field is missing.");
            var animator = serialized.FindProperty("m_Animator")
                ?? throw new InvalidOperationException("Player Animator field is missing.");
            var previous = (Transform)model.objectReferenceValue;
            if (previous == null || previous.parent != player.transform)
                throw new InvalidOperationException("Player Model must refer to its visual child.");
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(ready, player.transform)
                ?? throw new InvalidOperationException("Could not instantiate the prepared character.");
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
