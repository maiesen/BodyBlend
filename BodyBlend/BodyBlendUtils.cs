using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BodyBlend.Utils
{
	public static class BodyBlendUtils
	{
		public static NestedDictionaryList<string, string, BlendControlTemplate> RegisteredSkinBlendControls
			= new NestedDictionaryList<string, string, BlendControlTemplate>();
		internal static HashSet<string> PartsList = new HashSet<string>();
		private static Dictionary<string, string> SkinNameMapping = new Dictionary<string, string>();

		public static void ClearRegister()
		{
			RegisteredSkinBlendControls.Clear();
			PartsList.Clear();
		}

		public static string GetSkinName(string skinNameToken)
		{
			return SkinNameMapping[skinNameToken];
		}

		public static void RegisterFromJson(string skinNameToken, TextAsset jsonFile)
		{
			BBJsonConfig jsonConfig = JsonUtility.FromJson<BBJsonConfig>(jsonFile.text);
			if (string.IsNullOrEmpty(jsonConfig.skinName))
			{
				SkinNameMapping[skinNameToken] = skinNameToken;
			}
			else
			{
				SkinNameMapping[skinNameToken] = jsonConfig.skinName;
			}


			var templatesDict = jsonConfig.ToTemplates();

			foreach (var template in templatesDict)
			{
				RegisterSkinBlendControl(skinNameToken, template.Key, template.Value);
			}
		}

		public static void RegisterSkinBlendControl(string skinNameToken, string blendName, List<BlendControlTemplate> templates)
		{
			if (!RegisteredSkinBlendControls.ContainsKey(skinNameToken))
			{
				RegisteredSkinBlendControls[skinNameToken] = new DictionaryList<string, BlendControlTemplate>();
			}

			if (!RegisteredSkinBlendControls[skinNameToken].ContainsKey(blendName))
			{
				RegisteredSkinBlendControls[skinNameToken][blendName] = new List<BlendControlTemplate>();
			}

			RegisteredSkinBlendControls[skinNameToken][blendName] = templates;

			PartsList.Add(blendName);
		}

		public static void RegisterSkinBlendControl(string skinNameToken, string blendName, BlendControlTemplate template)
		{
			RegisterSkinBlendControl(skinNameToken, blendName, new List<BlendControlTemplate>() { template });
		}

		public class DictionaryList<K1, V> :
			Dictionary<K1, List<V>>
		{ }
		public class NestedDictionaryList<K1, K2, V> :
			Dictionary<K1, DictionaryList<K2, V>>
		{ }

		public class NestedDictionary<K1, K2, V> :
			Dictionary<K1, Dictionary<K2, V>>
		{ }

		public static TV GetValue<TK, TV>(this IDictionary<TK, TV> dict, TK key, TV defaultValue = default(TV))
		{
			TV value;
			return dict.TryGetValue(key, out value) ? value : defaultValue;
		}

		public class BlendControlTemplate
		{
			// Which mesh renderer to target at.
			public int targetRendererIndex = -1;
			// Dictionary for describing the blendShape values over weight values
			public Dictionary<int, AnimationCurve> blendShapeControls = new Dictionary<int, AnimationCurve>();

			// List of bones + curves to be used for controlling the dynamic bones
			public List<BoneControlTemplate> boneControls = new List<BoneControlTemplate>();
		}

		public class BoneControlTemplate
		{
			public List<string> associatedDynBoneNames = new List<string>();
			public AnimationCurve dynBoneInertCurve = null;
			public AnimationCurve dynBoneElasticityCurve = null;
			public AnimationCurve dynBoneStiffnessCurve = null;
			public AnimationCurve dynBoneDampingCurve = null;
			public DynBoneControlMode dynBoneControlMode = DynBoneControlMode.FULL_CONTROL;
		}

		//public static void ApplyOnlyBonesFromRegisteredBlendControls(this BodyBlendController controller, GameObject modelObject, string skinNameToken)
		//{
		//	if (!controller)
		//		return;

		//	var characterModel = modelObject.GetComponent<CharacterModel>();
		//	if (!characterModel) return;

		//	if (RegisteredSkinBlendControls.ContainsKey(skinNameToken))
		//	{
		//		foreach (var item in RegisteredSkinBlendControls[skinNameToken])
		//		{
		//			controller.ApplyOnlyBonesFromTemplates(characterModel, item.Key, item.Value);
		//		}
		//	}
		//}

		internal static void ApplyFromRegisteredBlendControls(this BodyBlendController controller, GameObject modelObject, string skinNameToken)
		{
			if (!controller)
				return;

			var characterModel = modelObject.GetComponent<CharacterModel>();
			if (!characterModel) return;

			if (RegisteredSkinBlendControls.ContainsKey(skinNameToken))
			{
				controller.SetSkinNameToken(skinNameToken);
				foreach (var item in RegisteredSkinBlendControls[skinNameToken])
				{
					Debug.Log($"[BodyBlend] Applying {skinNameToken}: {item.Key} to BodyBlendController");
					controller.ApplyFromTemplates(characterModel, item.Key, item.Value);
				}
			}
		}

		private static void ApplyFromTemplates(this BodyBlendController controller, CharacterModel charModel, string blendName, List<BlendControlTemplate> templates)
		{
			foreach (var template in templates)
			{
				if (template == null || template.targetRendererIndex < 0)
					return;

				var renderer = GetRenderer(charModel, template.targetRendererIndex);
				if (renderer)
				{
					BodyBlendControl control = new BodyBlendControl(renderer);
					control.blendShapeControls = template.blendShapeControls;

					if (template.boneControls != null && template.boneControls.Count > 0)
					{
						foreach (var boneControl in template.boneControls)
						{
							var dynamicBones = FindDynamicBones(charModel, boneControl.associatedDynBoneNames);
							control.AddDynBoneControl(
								dynamicBones,
								boneControl.dynBoneInertCurve,
								boneControl.dynBoneElasticityCurve,
								boneControl.dynBoneStiffnessCurve,
								boneControl.dynBoneDampingCurve,
								boneControl.dynBoneControlMode
							);
						}
					}

					controller.AddBlendControl(blendName, control);
				}
			}
		}

		public static bool HasRegisteredSkinControl(string name)
		{
			return RegisteredSkinBlendControls.ContainsKey(name);
		}

		public static List<DynamicBone> FindDynamicBones(CharacterModel charModel, params string[] names)
		{
			var dynamicBones = new List<DynamicBone>();
			foreach (var name in names)
			{
				var bone = FindDynamicBone(charModel, name);
				if (bone)
					dynamicBones.Add(bone);
			}
			return dynamicBones;
		}

		public static List<DynamicBone> FindDynamicBones(CharacterModel charModel, List<string> names)
		{
			var dynamicBones = new List<DynamicBone>();
			foreach (var name in names)
			{
				var bone = FindDynamicBone(charModel, name);
				if (bone)
					dynamicBones.Add(bone);
			}
			return dynamicBones;
		}

		public static DynamicBone FindDynamicBone(CharacterModel charModel, string boneName)
		{
			GameObject boneObject = charModel.gameObject.GetComponentsInChildren<Transform>()
										.FirstOrDefault(c => c.gameObject.name == boneName)?.gameObject;

			if (boneObject)
			{
				return boneObject.GetComponent<DynamicBone>();
			}
			return null;
		}

		public static SkinnedMeshRenderer GetRenderer(CharacterModel charModel, int index)
		{
			if (index < charModel.baseRendererInfos.Length)
				return (SkinnedMeshRenderer)charModel.baseRendererInfos[index].renderer;
			return null;
		}

		public static AnimationCurve MakeAnimationCurve(params Keyframe[] keyframes)
		{
			return new AnimationCurve(keyframes)
			{
				postWrapMode = WrapMode.ClampForever,
				preWrapMode = WrapMode.ClampForever
			};
		}
	}
}