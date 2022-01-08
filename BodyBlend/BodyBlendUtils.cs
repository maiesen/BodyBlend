using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BodyBlend.Utils
{
	public class BodyBlendUtils
	{
		private static NestedDictionaryList<string, string, BlendControlTemplate> RegisteredSkinBlendControls
			= new NestedDictionaryList<string, string, BlendControlTemplate>();

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
			// TODO: Consider cases where appending to templates is preferable
			RegisteredSkinBlendControls[skinNameToken][blendName] = templates;
		}

		public static void RegisterSkinBlendControl(string skinNameToken, string blendName, BlendControlTemplate template)
		{
			RegisterSkinBlendControl(skinNameToken, blendName, new List<BlendControlTemplate>() { template });
		}

		class DictionaryList<K1, V> :
			Dictionary<K1, List<V>>
		{ }
		class NestedDictionaryList<K1, K2, V> :
			Dictionary<K1, DictionaryList<K2, V>>
		{ }

		public class BlendControlTemplate
		{
			// Which mesh renderer to target at.
			public int targetRendererIndex = -1;
			// Dictionary for describing the blendShape values over weight values
			public Dictionary<int, AnimationCurve> blendShapeControls = new Dictionary<int, AnimationCurve>();
			// Mode for calculating target weight
			public WeightMode targetWeightMode = WeightMode.MAXIMUM;

			// This is for controlling how inert the bone is as weight gets updated
			// Which bones will be affected. Currently only matching the first appearances in the tree.
			public List<String> associatedDynBoneNames = new List<String>();
			public AnimationCurve dynBoneInertCurve = null;
			public AnimationCurve dynBoneElasticityCurve = null;
			public AnimationCurve dynBoneStiffnessCurve = null;
			public AnimationCurve dynBoneDampingCurve = null;
			public DynBoneControlMode dynBoneControlMode = DynBoneControlMode.BASE_TO_ONE;


			public bool useLerp = true;
			public float lerpSpeed = 1.0f;

			public float boneUpdateInterval = 0.015f;
		}

		public static void ApplyFromRegisteredBlendControls(BodyBlendController controller, GameObject modelObject, string skinNameToken)
		{
			if (!controller)
				return;

			var characterModel = modelObject.GetComponent<CharacterModel>();
			if (!characterModel) return;

			if (RegisteredSkinBlendControls.ContainsKey(skinNameToken))
			{
				foreach (var item in RegisteredSkinBlendControls[skinNameToken])
				{
					ApplyFromTemplates(controller, characterModel, item.Key, item.Value);
				}
			}
		}

		private static void ApplyFromTemplates(BodyBlendController controller, CharacterModel charModel, string blendName, List<BlendControlTemplate> templates)
		{
			foreach (var item in templates)
			{
				if (item.targetRendererIndex < 0)
					return;
				var renderer = GetRenderer(charModel, item.targetRendererIndex);
				if (renderer)
				{
					BodyBlendControl control = new BodyBlendControl(renderer);
					control.blendShapeControls = item.blendShapeControls;
					control.targetWeightMode = item.targetWeightMode;

					var dynamicBones = FindDynamicBones(charModel, item.associatedDynBoneNames);
					control.SetAssociatedDynBones(
						dynamicBones,
						item.dynBoneInertCurve,
						item.dynBoneElasticityCurve,
						item.dynBoneStiffnessCurve,
						item.dynBoneDampingCurve);
					control.dynBoneControlMode = item.dynBoneControlMode;

					control.SetLerp(item.useLerp, item.lerpSpeed);

					control.SetBoneUpdateInterval(item.boneUpdateInterval);

					controller.AddBlendControl(blendName, control);
				}
			}
		}

		public static bool HasRegisteredSkinControl(String name)
		{
			return RegisteredSkinBlendControls.ContainsKey(name);
		}

		public static List<DynamicBone> FindDynamicBones(CharacterModel charModel, params String[] names)
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

		public static DynamicBone FindDynamicBone(CharacterModel charModel, String boneName)
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