#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Editor utility that creates the FPS Animator Controller wired to
/// FPS_Character.fbx clips and compatible with GunScript's parameters.
///
/// HOW TO USE:
///   Unity menu → Tools → Deer Hunter → Build FPS Animator Controller
///
/// What it creates:
///   Assets/Animations/FPS_AnimatorController.controller
///
/// Parameters created (must match GunScript exactly):
///   float  walkSpeed    — driven by rb.velocity.magnitude
///   bool   aiming       — driven by Fire2 axis
///   bool   reloading    — driven by R key
///   bool   meeleAttack  — driven by Q key
///   int    maxSpeed     — 3 = walk, 5 = run
///
/// States created:
///   Idle        → Character_Idle        (loop)
///   Walk        → Character_Walk        (loop)
///   Run         → Character_Run         (loop)
///   Reload      → Character_Reload      (no loop) — name "Player_Reload"
///   Aim         → Character_AimPose     (loop)    — name "Player_AImpose"
///   Melee       → Character_Malee       (no loop) — name "Character_Malee"
///   GunTakeout  → Character_GUnTakeout  (no loop)
/// </summary>
public class FPSAnimatorControllerBuilder
{
    [MenuItem("Tools/Deer Hunter/Build FPS Animator Controller")]
    public static void Build()
    {
        // ── Find animation clips from FPS_Character.fbx ──────────
        string fbxPath = "Assets/Model_Animations_Textures/main character/FPS_Character.fbx";
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);

        AnimationClip idle       = FindClip(assets, "Character_Idle");
        AnimationClip walk       = FindClip(assets, "Character_Walk");
        AnimationClip run        = FindClip(assets, "Character_Run");
        AnimationClip reload     = FindClip(assets, "Character_Reload");
        AnimationClip melee      = FindClip(assets, "Character_Malee");
        AnimationClip aim        = FindClip(assets, "Character_AimPose");
        AnimationClip gunTakeout = FindClip(assets, "Character_GUnTakeout");

        // Warn about missing clips but continue — states still created
        Check(idle,       "Character_Idle");
        Check(walk,       "Character_Walk");
        Check(run,        "Character_Run");
        Check(reload,     "Character_Reload");
        Check(melee,      "Character_Malee");
        Check(aim,        "Character_AimPose");
        Check(gunTakeout, "Character_GUnTakeout");

        // ── Create controller asset ───────────────────────────────
        System.IO.Directory.CreateDirectory("Assets/Animations");
        string ctrlPath = "Assets/Animations/FPS_AnimatorController.controller";

        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);

        // ── Parameters ───────────────────────────────────────────
        ctrl.AddParameter("walkSpeed",   AnimatorControllerParameterType.Float);
        ctrl.AddParameter("aiming",      AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("reloading",   AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("meeleAttack", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("maxSpeed",    AnimatorControllerParameterType.Int);
        ctrl.AddParameter("changingWeapon", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        // ── States ───────────────────────────────────────────────
        // Layout positions for readability in the Animator window
        AnimatorState stIdle  = AddState(sm, "Idle",         idle,       new Vector3(-200, 0));
        AnimatorState stWalk  = AddState(sm, "Walk",         walk,       new Vector3( 200, -150));
        AnimatorState stRun   = AddState(sm, "Run",          run,        new Vector3( 200,  100));
        AnimatorState stAim   = AddState(sm, "Player_AImpose", aim,      new Vector3(-200, -250));
        AnimatorState stRld   = AddState(sm, "Player_Reload", reload,    new Vector3( 200, -400));
        AnimatorState stMelee = AddState(sm, "Character_Malee", melee,   new Vector3(-200,  250));
        AnimatorState stTake  = AddState(sm, "GunTakeout",   gunTakeout, new Vector3( 600,    0));

        // Default state
        sm.defaultState = stIdle;

        // ── Transitions ──────────────────────────────────────────
        // From Idle
        AddTransition(stIdle, stTake,  new Cond("meeleAttack", false, true),   0.1f);
        AddTransition(stIdle, stMelee, new Cond("meeleAttack", true,  true),   0.1f);
        AddTransition(stIdle, stAim,   new Cond("aiming",      true,  true),   0.15f);
        AddTransition(stIdle, stRld,   new Cond("reloading",   true,  true),   0.1f);
        AddSpeedTransition(stIdle, 4f, true, stRun);
        AddTransitionWalk(stIdle, stWalk, minWalk: 0.3f);

        // From Walk
        AddTransition(stWalk, stMelee, new Cond("meeleAttack", true,  true),   0.1f);
        AddTransition(stWalk, stAim,   new Cond("aiming",      true,  true),   0.15f);
        AddTransition(stWalk, stRld,   new Cond("reloading",   true,  true),   0.1f);
        AddSpeedTransition(stWalk, 4f, true, stRun);
        AddTransitionWalk(stWalk, stIdle, maxWalk: 0.3f);

        // From Run
        AddTransition(stRun, stMelee,  new Cond("meeleAttack", true,  true),   0.1f);
        AddTransition(stRun, stAim,    new Cond("aiming",      true,  true),   0.15f);
        AddTransition(stRun, stRld,    new Cond("reloading",   true,  true),   0.1f);
        AddSpeedTransition(stRun, 4f, false, stWalk);

        // changingWeapon → GunTakeout from any locomotion state
        AddTransition(stIdle, stTake,  new Cond("changingWeapon", true, true), 0.1f);
        AddTransition(stWalk, stTake,  new Cond("changingWeapon", true, true), 0.1f);
        AddTransition(stRun,  stTake,  new Cond("changingWeapon", true, true), 0.1f);

        // From Aim
        AddTransition(stAim, stRld,    new Cond("reloading",   true,  true),   0.1f);
        AddTransition(stAim, stIdle,   new Cond("aiming",      false, true),   0.15f);

        // From Reload (return to idle when done)
        var rldExit = stRld.AddExitTransition();
        rldExit.hasExitTime          = true;
        rldExit.exitTime             = 1f;
        rldExit.duration             = 0.15f;
        rldExit.AddCondition(AnimatorConditionMode.IfNot, 0, "reloading");

        // From Melee (return when done)
        var meleeExit = stMelee.AddExitTransition();
        meleeExit.hasExitTime        = true;
        meleeExit.exitTime           = 1f;
        meleeExit.duration           = 0.15f;
        meleeExit.AddCondition(AnimatorConditionMode.IfNot, 0, "meeleAttack");

        // From GunTakeout: return to idle when done
        var takeExit = stTake.AddExitTransition();
        takeExit.hasExitTime = true;
        takeExit.exitTime    = 1f;
        takeExit.duration    = 0.1f;
        takeExit.AddCondition(AnimatorConditionMode.IfNot, 0, "changingWeapon");

        // ── Save ─────────────────────────────────────────────────
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "FPS Animator Controller Built",
            "Created: Assets/Animations/FPS_AnimatorController.controller\n\n" +
            "NEXT STEPS:\n" +
            "1. Select your Player's hands/arms GameObject (the one with the Animator component)\n" +
            "2. Drag FPS_AnimatorController into its Animator → Controller slot\n" +
            "3. In GunScript Inspector, confirm:\n" +
            "   • reloadAnimationName  = Player_Reload\n" +
            "   • aimingAnimationName  = Player_AImpose\n" +
            "   • meeleAnimationName   = Character_Malee\n" +
            "4. Assign the Animator to GunScript → handsAnimator slot",
            "OK");

        Debug.Log("[FPSAnimatorControllerBuilder] Done — Assets/Animations/FPS_AnimatorController.controller");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
    }

    // ── Helpers ──────────────────────────────────────────────────

    static AnimationClip FindClip(Object[] assets, string name)
    {
        foreach (var a in assets)
            if (a is AnimationClip c && c.name == name) return c;
        return null;
    }

    static void Check(AnimationClip c, string name)
    {
        if (c == null) Debug.LogWarning($"[FPSAnimatorControllerBuilder] Clip not found: {name}");
    }

    static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip, Vector3 pos)
    {
        AnimatorState s = sm.AddState(name, pos);
        if (clip != null) s.motion = clip;
        return s;
    }

    struct Cond { public string param; public bool value; public bool isBool;
        public Cond(string p, bool v, bool ib) { param = p; value = v; isBool = ib; } }

    static void AddTransition(AnimatorState from, AnimatorState to, Cond cond, float duration)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration    = duration;
        t.AddCondition(cond.value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, cond.param);
    }

    // walkSpeed > threshold → dest
    static void AddSpeedTransition(AnimatorState from, float threshold, bool greater, AnimatorState dest)
    {
        var t = from.AddTransition(dest);
        t.hasExitTime = false;
        t.duration    = 0.15f;
        t.AddCondition(greater ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, threshold, "walkSpeed");
    }

    static void AddTransitionWalk(AnimatorState from, AnimatorState to, float minWalk = -1, float maxWalk = -1)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration    = 0.15f;
        if (minWalk >= 0) t.AddCondition(AnimatorConditionMode.Greater, minWalk, "walkSpeed");
        if (maxWalk >= 0) t.AddCondition(AnimatorConditionMode.Less,    maxWalk, "walkSpeed");
    }
}
#endif