﻿using BepInEx;
using BodyBlend.Utils;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Reflection;
using UnityEngine;

namespace BodyBlend
{
	[BepInDependency(SuspiciousTentacle.SuspiciousTentacle.PluginGUID, BepInDependency.DependencyFlags.SoftDependency)]
	[BepInPlugin(GUID, MODNAME, VERSION)]
	public sealed class BodyBlendPlugin : BaseUnityPlugin
	{
		public const string
				MODNAME = "BodyBlend",
				AUTHOR = "Maiesen",
				GUID = "com." + AUTHOR + "." + MODNAME,
				VERSION = "0.2.0";

		private void Awake() //Called when loaded by BepInEx.
		{
			// Hook after the usual SkinDef Apply to make sure all dynamic bones have been loaded first
			IL.RoR2.ModelSkinController.ApplySkin += ILModelSkinControllerApplySkin;
			IL.RoR2.UI.CharacterSelectController.OnNetworkUserLoadoutChanged += ILCharacterSelectControllerApplySkin;

			if (SuspiciousTentacleCompatibility.enabled)
				SuspiciousTentacleCompatibility.HookGrowthProgress();

			//PreRegisterSkins();
		}

		private void Start() //Called at the first frame of the game.
		{

		}

		#region Skin Preregistration
		// For registering skin blends for Maiesen skin mods
		//private void PreRegisterSkins()
		//{

		//}

		//private void RegisterPlagueArtificer()
		//{
		//	BlendControlTemplate template = new BlendControlTemplate();
		//	template.targetRendererIndex = 3;
		//	template.blendShapeControls[0] = MakeAnimationCurve(
		//		new Keyframe(0f, 0f),
		//		new Keyframe(1f, 1f));
		//	template.blendShapeControls[1] = MakeAnimationCurve(
		//		new Keyframe(0f, 0f),
		//		new Keyframe(0.3f, 1f, 0f, 0f),
		//		new Keyframe(1f, 0f));

		//	template.associatedDynBoneNames.AddRange(new string[] { "belly" });
		//	template.dynBoneCurve = MakeAnimationCurve(
		//			new Keyframe(0f, 1f),
		//			new Keyframe(0.25f, 1f),
		//			new Keyframe(1f, 0f));

		//	template.lerpSpeed = 2.0f;

		//	RegisterSkinBlendControl("PlagueArtificerSkin", "Belly", template);
		//}
		#endregion

		private const BindingFlags AllFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
																					BindingFlags.Instance | BindingFlags.DeclaredOnly;

		public static void SkinDefApply(Action<SkinDef, GameObject> orig, SkinDef self, GameObject modelObject)
		{
			orig(self, modelObject);

			var previousController = modelObject.GetComponent<BodyBlendController>();
			if (previousController)
				Destroy(previousController);

			//Debug.Log("Apply Skin Def in BodyBlend: " + self.name);
			//Debug.Log("SkinDef name token: " + self.nameToken);

			// Skin must be registered using nameToken for 
			if (!BodyBlendUtils.HasRegisteredSkinControl(self.nameToken)) return;

			var controller = modelObject.AddComponent<BodyBlendController>();
			BodyBlendUtils.ApplyFromRegisteredBlendControls(controller, modelObject, self.nameToken);
		}

		internal static void ILModelSkinControllerApplySkin(ILContext il)
		{
			ILCursor c = new ILCursor(il);
			var ILFound = c.TryGotoNext(
				MoveType.After,
				x => x.MatchLdarg(0),
				x => x.MatchCall<Component>("get_gameObject"),
				x => x.MatchCallvirt<SkinDef>(nameof(SkinDef.Apply))
				);
			if (ILFound)
			{
				c.Emit(OpCodes.Ldarg_0);
				c.Emit(OpCodes.Ldfld, typeof(ModelSkinController)
					.GetField(nameof(ModelSkinController.skins), AllFlags));
				c.Emit(OpCodes.Ldarg_1);
				c.Emit(OpCodes.Ldelem_Ref);
				c.Emit(OpCodes.Ldarg_0);
				c.Emit(OpCodes.Call, typeof(Component).GetMethod("get_gameObject"));
				c.EmitDelegate<Action<SkinDef, GameObject>>(SetUpBodyBlend);
			}
		}

		internal static void ILCharacterSelectControllerApplySkin(ILContext il)
		{
			ILCursor c = new ILCursor(il);
			int localSkinDefIndex = -1;
			int localGameObjectIndex = -1;

			var ILFound = c.TryGotoNext(
				MoveType.After,
				x => x.MatchLdloc(out localSkinDefIndex),
				x => x.MatchLdloc(out localGameObjectIndex),
				x => x.MatchCallvirt<Component>("get_gameObject"),
				x => x.MatchCall<SkinDef>(nameof(SkinDef.Apply))
				);
			if (ILFound)
			{
				c.Emit(OpCodes.Ldloc_S, (byte) localSkinDefIndex);
				c.Emit(OpCodes.Ldloc_S, (byte) localGameObjectIndex);
				c.Emit(OpCodes.Callvirt, typeof(Component).GetMethod("get_gameObject"));
				c.EmitDelegate<Action<SkinDef, GameObject>>(SetUpBodyBlend);
			}
		}

		public static void SetUpBodyBlend(SkinDef skinDef, GameObject model)
		{
			var previousController = model.GetComponent<BodyBlendController>();
			if (previousController)
				Destroy(previousController);

			// Skin must be registered using nameToken for 
			if (!BodyBlendUtils.HasRegisteredSkinControl(skinDef.nameToken)) return;

			var controller = model.AddComponent<BodyBlendController>();
			BodyBlendUtils.ApplyFromRegisteredBlendControls(controller, model, skinDef.nameToken);
		}
	}
}
