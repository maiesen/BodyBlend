using BepInEx;
using BodyBlend.Utils;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Reflection;
using UnityEngine;
using BepInEx.Configuration;
using System.Collections.Generic;
using System.IO;
using RiskOfOptions;

namespace BodyBlend
{
	[BepInDependency(SuspiciousTentacle.SuspiciousTentacle.PluginGUID, BepInDependency.DependencyFlags.SoftDependency)]
	[BepInDependency("com.rune580.riskofoptions")]
	[BepInPlugin(GUID, MODNAME, VERSION)]
	public sealed class BodyBlendPlugin : BaseUnityPlugin
	{
		public const string
				MODNAME = "BodyBlend",
				AUTHOR = "Maiesen",
				GUID = "com." + AUTHOR + "." + MODNAME,
				VERSION = "0.3.1";

		private void Awake() //Called when loaded by BepInEx.
		{
			// Hook after the usual SkinDef Apply to make sure all dynamic bones have been loaded first
			IL.RoR2.ModelSkinController.ApplySkin += ILModelSkinControllerApplySkin;
			//IL.RoR2.UI.CharacterSelectController.OnNetworkUserLoadoutChanged += ILCharacterSelectControllerApplySkin;

			if (SuspiciousTentacleCompatibility.enabled)
				SuspiciousTentacleCompatibility.HookGrowthProgress();

		}

		private void Start() //Called at the first frame of the game.
		{
			var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			using (var memoryStream = new MemoryStream())
			{
				Assembly.GetExecutingAssembly().GetManifestResourceStream("BodyBlend.icon.png").CopyTo(memoryStream);
				texture.LoadImage(memoryStream.ToArray());
			}
			var icon = Sprite.Create(texture, new Rect(0f, 0f, texture.height, texture.width), new Vector2(0.5f, 0.5f), 100);
			ModSettingsManager.SetModIcon(icon);

			RoR2Application.onLoad += OnLoad;
			SearchConfigJson();
		}

		private void OnLoad()
		{
			// Try to register from json
			foreach(var skinDef in SkinCatalog.allSkinDefs) {
				LoadBodyBlendJson(skinDef);
			}

			// Create/Load configs to RiskOfOptions
			BodyBlendOptions.InitializeOptions(Config);
		}

		public static void ReloadJson()
		{
			Debug.Log("[BodyBlend] Reloading config files.");
			SearchConfigJson();

			BodyBlendUtils.ClearRegister();
			// Reload from Json, should overwrite current settings
			foreach (var skinDef in SkinCatalog.allSkinDefs)
			{
				LoadBodyBlendJson(skinDef, overwrite: true);
			}
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

		/*
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
				c.Emit(OpCodes.Ldloc_S, (byte)localSkinDefIndex);
				c.Emit(OpCodes.Ldloc_S, (byte)localGameObjectIndex);
				c.Emit(OpCodes.Callvirt, typeof(Component).GetMethod("get_gameObject"));
				c.EmitDelegate<Action<SkinDef, GameObject>>(SetUpBoneOnly);
			}
		}
		*/

		public static void SetUpBodyBlend(SkinDef skinDef, GameObject model)
		{
			if (skinDef.nameToken == "") return;

			var previousController = model.GetComponent<BodyBlendController>();
			if (previousController)
			{
				Destroy(previousController);
			}

			// Skin must be registered using nameToken for key
			if (!BodyBlendUtils.HasRegisteredSkinControl(skinDef.nameToken))
			{
				return;
			}

			var controller = model.AddComponent<BodyBlendController>();
			controller.ApplyFromRegisteredBlendControls(model, skinDef.nameToken);
		}

		//public static void SetUpBoneOnly(SkinDef skinDef, GameObject model)
		//{
		//	var previousController = model.GetComponent<BodyBlendController>();
		//	if (previousController)
		//	{
		//		Destroy(previousController);
		//	}

		//	if (!BodyBlendUtils.HasRegisteredSkinControl(skinDef.nameToken))
		//	{
		//		return;
		//	}

		//	var controller = model.AddComponent<BodyBlendController>();
		//	controller.ApplyOnlyBonesFromRegisteredBlendControls(model, skinDef.nameToken);
		//}

		// Try to load from json into registered skin control dict
		private static void LoadBodyBlendJson(SkinDef skinDef, bool overwrite = false)
		{
			if (BodyBlendUtils.HasRegisteredSkinControl(skinDef.nameToken) && !overwrite)
				return;

			if (!FoundJson.ContainsKey($"{skinDef.nameToken}.json"))
				return;

			var path = FoundJson[$"{skinDef.nameToken}.json"];
			Debug.Log($"[BodyBlend] Loading {skinDef.nameToken} BodyBlend config from json.");
			TextAsset jsonFile = new TextAsset(File.ReadAllText(path));

			if (jsonFile != null)
			{
				BodyBlendUtils.RegisterFromJson(skinDef.nameToken, jsonFile);
			}
		}

		private static Dictionary<string, string> FoundJson = new Dictionary<string, string>();

		// Search and returns path
		private static void SearchConfigJson()
		{
			FoundJson.Clear();

			var bepinexDir = BepInEx.Paths.BepInExAssemblyDirectory;
			string pluginsDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(bepinexDir, @"..\plugins\"));
			var files = Directory.GetFiles(pluginsDir, "*.json", SearchOption.AllDirectories);

			foreach (var file in files)
			{
				if (File.Exists(file))
				{
					if (System.IO.Path.GetFileName(file) == "manifest.json") continue;
					FoundJson[System.IO.Path.GetFileName(file)] = file;
				}
			}
		}
	}
}
