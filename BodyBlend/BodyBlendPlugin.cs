using BepInEx;
using MonoMod.RuntimeDetour;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using R2API;
using R2API.Utils;
using BodyBlend.Utils;
using OnRoR2 = On.RoR2;
using static BodyBlend.Utils.BodyBlendUtils;

namespace BodyBlend
{
  //[BepInDependency("com.bepis.r2api")]
  //[R2APISubmoduleDependency(nameof(yourDesiredAPI))]
	[BepInDependency(SuspiciousTentacle.SuspiciousTentacle.PluginGUID, BepInDependency.DependencyFlags.SoftDependency)]
  [BepInPlugin(GUID, MODNAME, VERSION)]
  public sealed class BodyBlendPlugin : BaseUnityPlugin
  {
    public const string
        MODNAME = "BodyBlend",
        AUTHOR = "Maiesen",
        GUID = "com." + AUTHOR + "." + MODNAME,
        VERSION = "0.1.0";

		private void Awake() //Called when loaded by BepInEx.
    {
			new Hook(
				typeof(SkinDef).GetMethod(nameof(SkinDef.Apply)),
				typeof(BodyBlendPlugin).GetMethod(nameof(SkinDefApply)),
				new HookConfig()
				{
					// Should make this method get called after bone modifications are complete.
					Priority = 99
				});

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

		public static void SkinDefApply(Action<SkinDef, GameObject> orig, SkinDef self, GameObject modelObject)
		{
			orig(self, modelObject);

			var previousController = modelObject.GetComponent<BodyBlendController>();
			if (previousController)
				Destroy(previousController);

			Debug.Log("Apply Skin Def in BodyBlend: " + self.name);
			Debug.Log("SkinDef name token: " + self.nameToken);

			// Skin must be registered using nameToken for 
			if (!BodyBlendUtils.HasRegisteredSkinControl(self.nameToken)) return;

			var controller = modelObject.AddComponent<BodyBlendController>();
			BodyBlendUtils.ApplyFromRegisteredBlendControls(controller, modelObject, self.nameToken);
		}
  }
}
