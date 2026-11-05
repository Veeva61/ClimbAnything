using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppHexabodyVR.PlayerController;
using Il2CppHurricaneVR.Framework.Core.Player;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(ClimbAnything.Core), "ClimbAnything", "1.0.0", "geom2543")]
[assembly: MelonGame("Kubunautilus", "BrutalistickVR")]

namespace ClimbAnything
{
    // Grip next to any solid surface -> the physics hand is pinned to it with a joint and HexaBody is told
    // the hand is "Climbing", so the game's own climbing strength and body logic do the rest.
    public class Core : MelonMod
    {
        private MelonPreferences_Category _cat;
        internal static MelonPreferences_Entry<bool> Enabled;
        private MelonPreferences_Entry<float> _radius;
        private MelonPreferences_Entry<bool> _allowDynamic;
        private MelonPreferences_Entry<float> _minDynamicMass;
        private MelonPreferences_Entry<float> _breakForce;
        private MelonPreferences_Entry<bool> _verbose;

        private class HandState
        {
            public HVRHexaBodyHands Hand;
            public bool WasDown;
            public int Wait = -1;       // frames left before we try (lets the game grab real items first)
            public ConfigurableJoint Anchor;
        }

        private static readonly Dictionary<IntPtr, HandState> Hands = new();
        private static HexaBodyPlayer4 _body;
        private static Transform _rig;

        internal static bool IsAnchored(HVRHexaBodyHands h) =>
            h != null && Hands.TryGetValue(h.Pointer, out var s) && s.Anchor != null;

        public override void OnInitializeMelon()
        {
            _cat = MelonPreferences.CreateCategory("ClimbAnything");
            Enabled = _cat.CreateEntry("Enabled", true, "Enabled");
            _radius = _cat.CreateEntry("GrabRadius", 0.12f, "Grab radius (m)",
                "How close the hand must be to a surface for grip to catch it");
            _allowDynamic = _cat.CreateEntry("AllowDynamicObjects", false, "Climb loose objects",
                "Also catch on non-kinematic physics objects that have no grab handle");
            _minDynamicMass = _cat.CreateEntry("MinDynamicMass", 40f, "Min mass of loose objects (kg)");
            _breakForce = _cat.CreateEntry("BreakForce", 0f, "Grip break force (N)", "0 = never slips");
            _verbose = _cat.CreateEntry("Verbose", true, "Verbose log");
            LoggerInstance.Msg("Loaded. Grip next to any wall, ledge or ceiling to hold on.");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            Hands.Clear();
            _body = null;
            _rig = null;
        }

        private static bool FindBody()
        {
            if (_body != null && _body.WasCollected == false && _body) return true;
            _body = UnityEngine.Object.FindObjectOfType<HexaBodyPlayer4>();
            if (_body == null) return false;
            _rig = _body.transform.parent != null ? _body.transform.parent : _body.transform;
            Hands.Clear();
            foreach (var h in _rig.GetComponentsInChildren<HVRHexaBodyHands>(true))
                if (h != null) Hands[h.Pointer] = new HandState { Hand = h };
            return true;
        }

        public override void OnUpdate()
        {
            if (!Enabled.Value || !FindBody()) return;
            foreach (var s in Hands.Values)
            {
                try { UpdateHand(s); }
                catch (Exception e) { LoggerInstance.Warning("Hand update: " + e.Message); }
            }
        }

        private void UpdateHand(HandState s)
        {
            var h = s.Hand;
            if (h == null || h.WasCollected) return;
            var g = h.Grabber;
            if (g == null) return;
            var ctl = g.Controller;
            bool down = ctl != null && ctl.GripButtonState.Active;

            if (down && !s.WasDown && s.Anchor == null) s.Wait = 2;
            s.WasDown = down;

            if (!down) { Release(s, "grip released"); return; }

            // a real item got grabbed: the game handles it
            if (s.Anchor != null && g.IsGrabbing) { Release(s, "grabbed an item"); return; }

            if (s.Wait > 0) { s.Wait--; return; }
            if (s.Wait == 0)
            {
                s.Wait = -1;
                if (!g.IsGrabbing && g.HoverTarget == null) TryAnchor(s, g.IsLeftHand ? "L" : "R");
            }
        }

        private void TryAnchor(HandState s, string side)
        {
            var rb = s.Hand.RigidBody;
            if (rb == null) return;
            Vector3 c = rb.worldCenterOfMass;
            float r = _radius.Value * Mathf.Max(0.01f, _rig.lossyScale.x);

            Collider best = null;
            float bestD = float.MaxValue;
            foreach (var col in Physics.OverlapSphere(c, r, ~0, QueryTriggerInteraction.Ignore))
            {
                if (col == null || col.isTrigger) continue;
                if (col.transform.IsChildOf(_rig)) continue;
                var arb = col.attachedRigidbody;
                if (arb != null && !arb.isKinematic && !(_allowDynamic.Value && arb.mass >= _minDynamicMass.Value)) continue;
                var mc = col.TryCast<MeshCollider>(); // C# "is" doesn't work on Il2Cpp objects
                Vector3 p = mc != null && !mc.convex ? col.bounds.ClosestPoint(c) : col.ClosestPoint(c);
                float d = (p - c).sqrMagnitude;
                if (d < bestD) { bestD = d; best = col; }
            }
            if (best == null) return;

            var j = rb.gameObject.AddComponent<ConfigurableJoint>();
            var arb2 = best.attachedRigidbody;
            j.connectedBody = arb2; // null = the world
            j.autoConfigureConnectedAnchor = true;
            j.anchor = Vector3.zero;
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
            // wrist turns freely, the hand joint to the body still aims it at the controller
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Free;
            j.enableCollision = false;
            j.enablePreprocessing = false;
            if (_breakForce.Value > 0f) j.breakForce = _breakForce.Value;
            s.Anchor = j;
            if (_verbose.Value)
                LoggerInstance.Msg($"[{side}] holding '{best.name}' ({(arb2 == null ? "static" : arb2.isKinematic ? "kinematic" : "dynamic")}) at {Mathf.Sqrt(bestD):F2} m");
        }

        private void Release(HandState s, string why)
        {
            if (s.Anchor == null) return;
            UnityEngine.Object.Destroy(s.Anchor);
            s.Anchor = null;
            if (_verbose.Value) LoggerInstance.Msg("Let go: " + why);
        }
    }

    // While a hand is pinned by us, report it as climbing so HexaBody uses its climbing strength.
    [HarmonyPatch(typeof(HVRHexaBodyHands), nameof(HVRHexaBodyHands.GetHandState))]
    internal static class HandStatePatch
    {
        private static void Postfix(HVRHexaBodyHands __instance, ref HandGrabState __result)
        {
            if (Core.Enabled.Value && Core.IsAnchored(__instance)) __result = HandGrabState.Climbing;
        }
    }
}
