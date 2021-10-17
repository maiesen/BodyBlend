using BepInEx;
using MonoMod.RuntimeDetour;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using R2API;
using R2API.Utils;

namespace BodyBlend.Utils
{
	public class BodyBlendUtils
	{
		private static NestedDictionaryList<string, string, BlendControlTemplate> RegisteredSkinBlendControls
			= new NestedDictionaryList<string, string, BlendControlTemplate>();

		public static void RegisterSkinBlendControl(string skinName, string blendName, List<BlendControlTemplate> templates)
		{
			if (!RegisteredSkinBlendControls.ContainsKey(skinName))
			{
				RegisteredSkinBlendControls[skinName] = new DictionaryList<string, BlendControlTemplate>();
			}

			if (!RegisteredSkinBlendControls[skinName].ContainsKey(blendName))
			{
				RegisteredSkinBlendControls[skinName][blendName] = new List<BlendControlTemplate>();
			}

			RegisteredSkinBlendControls[skinName][blendName] = templates;
		}

		public static void RegisterSkinBlendControl(string skinName, string blendName, BlendControlTemplate template)
		{
			RegisterSkinBlendControl(skinName, blendName, new List<BlendControlTemplate>() { template });
		}

		class DictionaryList<K1, V> :
			Dictionary<K1, List<V>>{ }
		class NestedDictionaryList<K1, K2, V> :
			Dictionary<K1, DictionaryList<K2, V>>{ }

		public class BlendControlTemplate
		{
			// Which mesh to target at.
			public int targetRendererIndex = -1;
			public Dictionary<int, AnimationCurve> blendShapeControls = new Dictionary<int, AnimationCurve>();
			// Mode for calculating target weight
			public BodyBlendControl.WeightMode targetWeightMode = BodyBlendControl.WeightMode.MAXIMUM;

			// This is for controlling how inert the bone is as weight gets updated
			// Which bones will be affected. Currently only matching the first appearances in the tree.
			public List<String> associatedDynBoneNames = new List<String>();
			// Value of 1 is max Inert while 0 is min Inert
			public AnimationCurve dynBoneCurve = null;

			public bool useLerp = true;
			public float lerpSpeed = 1.0f;

			public float boneUpdateInterval = 0.015f;
		}

		public static void ApplyFromRegisteredBlendControls(BodyBlendController controller, GameObject modelObject, string skinName)
		{
			if (!controller)
				return;

			var characterModel = modelObject.GetComponent<CharacterModel>();
			if (!characterModel) return;

			if (RegisteredSkinBlendControls.ContainsKey(skinName))
			{
				foreach (var item in RegisteredSkinBlendControls[skinName])
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

					var dynamicBones = FindDynamicBones(charModel, item.associatedDynBoneNames);
					control.SetAssociatedDynBones(dynamicBones, item.dynBoneCurve);

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
				if(bone)
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
				return boneObject.gameObject.GetComponent<DynamicBone>();
			}
			return null;
		}

		public static SkinnedMeshRenderer GetRenderer(CharacterModel charModel, int index)
		{
			if (index < charModel.baseRendererInfos.Length)
				return (SkinnedMeshRenderer) charModel.baseRendererInfos[index].renderer;
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