using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static BodyBlend.Utils.BodyBlendUtils;

namespace BodyBlend
{
	[Serializable]
	class BBJsonConfig
	{
		public string skinName;
		public List<BBJsonPartControl> parts;
	}

	[Serializable]
	class BBJsonPartControl
	{
		public string name;
		public List<BBJsonCtrlTemplate> controls;
	}

	[Serializable]
	class BBJsonCtrlTemplate
	{
		public int targetRendererIndex = -1;
		public List<BBShapeControl> blendControls;
		public List<BBBoneControl> boneControls;
	}

	[Serializable]
	class BBBoneControl
	{
		public List<string> boneNames = new List<string>();
		public List<BBAnimKey> inertCurve;
		public List<BBAnimKey> elasticityCurve;
		public List<BBAnimKey> stiffnessCurve;
		public List<BBAnimKey> dampingCurve;
		public string controlMode = "FULL_CONTROL";
	}

	[Serializable]
	class BBShapeControl
	{
		public int blendIdx = -1;
		public List<BBAnimKey> keyframes;
	}

	[Serializable]
	class BBAnimKey
	{
		public List<float> val;
	}

	internal static class BBJsonExtensions
	{
		private static Dictionary<string, DynBoneControlMode> DynBoneDict = new Dictionary<string, DynBoneControlMode> {
				{ "ZERO_TO_BASE", DynBoneControlMode.ZERO_TO_BASE },
				{ "BASE_TO_ONE", DynBoneControlMode.BASE_TO_ONE },
				{ "FULL_CONTROL", DynBoneControlMode.FULL_CONTROL }
		};

		public static DictionaryList<string, BlendControlTemplate> ToTemplates(this BBJsonConfig self)
		{
			var templateDict = new DictionaryList<string, BlendControlTemplate>();
			foreach (var part in self.parts)
			{
				var templates = new List<BlendControlTemplate>();
				foreach (var control in part.controls)
				{
					var template = new BlendControlTemplate();

					template.targetRendererIndex = control.targetRendererIndex;
					template.blendShapeControls = control.blendControls.ToAnimDict();

					template.boneControls = control.boneControls.Select(bone => bone.ToTemplate()).Where(bone => bone != null).ToList();

					templates.Add(template);
				}
				templateDict.Add(part.name, templates);
			}
			return templateDict;
		}

		private static Dictionary<int, AnimationCurve> ToAnimDict(this List<BBShapeControl> self)
		{
			if (self == null || self.Count() < 1)
				return null;

			Dictionary<int, AnimationCurve> dict = new Dictionary<int, AnimationCurve>();

			foreach (var item in self)
			{
				dict.Add(item.blendIdx, item.keyframes.ToAnimationCurve());
			}

			return dict;
		}

		private static BoneControlTemplate ToTemplate(this BBBoneControl self)
		{
			if (self == null)
				return null;

			var template = new BoneControlTemplate();

			template.associatedDynBoneNames = self.boneNames;
			template.dynBoneInertCurve = self.inertCurve.ToAnimationCurve();
			template.dynBoneElasticityCurve = self.elasticityCurve.ToAnimationCurve();
			template.dynBoneStiffnessCurve = self.stiffnessCurve.ToAnimationCurve();
			template.dynBoneDampingCurve = self.dampingCurve.ToAnimationCurve();
			if (DynBoneDict.ContainsKey(self.controlMode))
				template.dynBoneControlMode = DynBoneDict[self.controlMode];

			return template;
		}

		private static AnimationCurve ToAnimationCurve(this List<BBAnimKey> self)
		{
			if (self == null || self.Count < 2)
				return null;

			var keyframes = self
				.Where(elem => elem.val.Count == 2 || elem.val.Count == 4 || elem.val.Count == 6)
				.Select(elem =>
				{
					var arr = elem.val;
					if (arr.Count == 2)
					{
						return new Keyframe(arr[0], arr[1]);
					}
					else if (arr.Count == 4)
					{
						return new Keyframe(arr[0], arr[1], arr[2], arr[3]);
					}
					else
					{
						return new Keyframe(arr[0], arr[1], arr[2], arr[3], arr[4], arr[5]);
					}
				}).ToArray();
			return MakeAnimationCurve(keyframes);
		}
	}
}
