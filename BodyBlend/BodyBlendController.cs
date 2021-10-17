using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BodyBlend
{
	[DisallowMultipleComponent]
	public class BodyBlendController : MonoBehaviour
	{
		private DictionaryList<String, BodyBlendControl> bodyBlendControls = new DictionaryList<String, BodyBlendControl>();

		class DictionaryList<K1, V> :
			Dictionary<K1, List<V>>{ }

		public void Awake()
		{

		}

		public void LateUpdate()
		{
			foreach (var item in bodyBlendControls.Values)
				foreach (var item2 in item)
					item2.Update(Time.deltaTime);
		}

		private float updateVal = 0.0f;
		private float updateVal2 = 0.0f;
		public void Update()
		{
			if (Input.GetKey(KeyCode.Period))
			{
				updateVal += 0.01f;
			}
			if (Input.GetKey(KeyCode.Comma))
			{
				updateVal -= 0.01f;
			}
			updateVal = Mathf.Clamp01(updateVal);

			if (Input.GetKey(KeyCode.L))
			{
				updateVal2 += 0.01f;
			}
			if (Input.GetKey(KeyCode.K))
			{
				updateVal2 -= 0.01f;
			}
			updateVal2 = Mathf.Clamp01(updateVal2);

			SetBlendTargetWeight("Belly", updateVal);
			SetBlendTargetWeight("Boobs", updateVal2);
		}

		public void SetBlendLerp(String name, bool enabled, float speed = 1.0f)
		{
			if (bodyBlendControls.ContainsKey(name))
			{
				foreach (var item in bodyBlendControls[name])
				{
					item.SetLerp(enabled, speed);
				}
			}

		}

		// Source is required for handling multiple set weight requests
		public void SetBlendTargetWeight(String name, float value, String source = "Default")
		{
			if (HasBlendControl(name))
			{
				foreach (var item in bodyBlendControls[name])
					item.SetTargetWeight(value, source);
			}
		}

		public bool HasBlendControl(String name)
		{
			return bodyBlendControls.ContainsKey(name);
		}

		public void AddBlendControl(String name, BodyBlendControl control)
		{
			if (!bodyBlendControls.ContainsKey(name))
				bodyBlendControls[name] = new List<BodyBlendControl>();
			bodyBlendControls[name].Add(control);
		}

		public void RemoveBlendControl(String name)
		{
			bodyBlendControls.Remove(name);
		}

		public List<BodyBlendControl> GetBlendControl(String name)
		{
			return bodyBlendControls[name];
		}
	}

	public class BodyBlendControl : UnityEngine.Object
	{
		private static readonly FieldInfo dynBoneParticlesField = typeof(DynamicBone).GetField("m_Particles", BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo dynBoneTotalLengthField = typeof(DynamicBone).GetField("m_BoneTotalLength", BindingFlags.NonPublic | BindingFlags.Instance);
		private struct DynBoneValue
		{
			public float m_Damping;
			public float m_Elasticity;
			public float m_Stiffness;
			public float m_Inert;

			public DynBoneValue (float d, float e, float s, float i)
			{
				m_Damping = d;
				m_Elasticity = e;
				m_Stiffness = s;
				m_Inert = i;
			}
		}

		public enum WeightMode
		{
			AVERAGE,
			MINIMUM,
			MAXIMUM
		}

		public Dictionary<int, AnimationCurve> blendShapeControls = new Dictionary<int, AnimationCurve>();
		// Mode for calculating target weight
		public WeightMode targetWeightMode = WeightMode.MAXIMUM;

		// This is for controlling how inert the bone is as weight gets updated
		private List<DynamicBone> associatedDynBones = null;
		// 1 is max Inert while 0 is min Inert
		private AnimationCurve dynBoneCurve = null;

		private SkinnedMeshRenderer targetRenderer;

		private bool useLerp = true;
		private float lerpSpeed = 1.0f;
		private float boneUpdateInterval = 0.015f;

		private List<DynBoneValue> defaultDynBoneValues = null;
		private float elapsedTime = 0f;
		private float currentWeight = 0f;
		private Dictionary<String, float> targetWeights = new Dictionary<String, float>(){{"Default", 0f}};

		public BodyBlendControl(SkinnedMeshRenderer targetRenderer = null)
		{
			this.targetRenderer = targetRenderer;
		}

		public void SetTargetRenderer(SkinnedMeshRenderer rederer)
		{
			targetRenderer = rederer;
		}

		public void SetLerp(bool enabled, float speed = 1.0f)
		{
			useLerp = enabled;
			lerpSpeed = speed;
		}

		public void SetAssociatedDynBones(List<DynamicBone> bones, AnimationCurve curve)
		{
			if (bones == null || bones.Count <= 0)
				return;
			associatedDynBones = bones;
			dynBoneCurve = curve;
			if (associatedDynBones != null)
				SaveDynBoneValues(associatedDynBones);
		}

		public void SetBoneUpdateInterval(float interval)
		{
			boneUpdateInterval = interval;
		}

		public void SetTargetWeight(float weight, String source)
		{
			// TODO: Consider case where blendshape go beyond range [0, 1]
			targetWeights[source] = Mathf.Clamp(weight, 0f, 1f);
		}

		public void Update(float t)
		{
			// Check if dynamic bones need updating
			elapsedTime += t;
			bool doBoneUpdate = false;
			if (boneUpdateInterval <= 0f)
			{
				doBoneUpdate = true;
			}
			else if (elapsedTime >= boneUpdateInterval)
			{
				elapsedTime -= boneUpdateInterval;
				elapsedTime = Mathf.Min(elapsedTime, boneUpdateInterval);
				doBoneUpdate = true;
			}

			if (useLerp)
				currentWeight = Mathf.Lerp(currentWeight, GetTargetWeight(), t * lerpSpeed);
			else
				currentWeight = GetTargetWeight();

			// Update BlendShapes
			foreach (var item in blendShapeControls)
			{
				float value = item.Value.Evaluate(currentWeight);
				targetRenderer.SetBlendShapeWeight(item.Key, value * 100f);
			}

			if (doBoneUpdate && associatedDynBones != null)
			{
				float value = dynBoneCurve.Evaluate(currentWeight);
				for (int i = 0; i < associatedDynBones.Count; i++)
				{
					associatedDynBones[i].m_Inert = Mathf.Lerp(defaultDynBoneValues[i].m_Inert, 1f, value);
					UpdateDynBoneParameters(associatedDynBones[i]);
				}
			}
		}

		private float GetTargetWeight()
		{
			switch(targetWeightMode)
			{
				case WeightMode.MAXIMUM:
					return targetWeights.Count > 0 ? targetWeights.Values.Max() : 0f;
				case WeightMode.MINIMUM:
					return targetWeights.Count > 0 ? targetWeights.Values.Min() : 0f;
				case WeightMode.AVERAGE:
					return targetWeights.Count > 0 ? targetWeights.Values.Average() : 0f;
			}
			return 0f;
		}

		private void SaveDynBoneValues(List<DynamicBone> source)
		{
			if (defaultDynBoneValues == null)
				defaultDynBoneValues = new List<DynBoneValue>();

			foreach (DynamicBone item in source)
			{
				defaultDynBoneValues.Add(new DynBoneValue(
					item.m_Damping,
					item.m_Elasticity,
					item.m_Stiffness,
					item.m_Inert
				));
			}
		}

		private static void UpdateDynBoneParameters(DynamicBone bone)
		{
			List<DynamicBone.Particle> particles = (List<DynamicBone.Particle>) dynBoneParticlesField.GetValue(bone);

			for (int i = 0; i < particles.Count; ++i)
			{
				DynamicBone.Particle p = particles[i];
				p.m_Damping = bone.m_Damping;
				p.m_Elasticity = bone.m_Elasticity;
				p.m_Stiffness = bone.m_Stiffness;
				p.m_Inert = bone.m_Inert;
				p.m_Radius = bone.m_Radius;

				var length = (float) dynBoneTotalLengthField.GetValue(bone);

				if (length > 0)
				{
					float a = p.m_BoneLength / length;
					if (bone.m_DampingDistrib != null && bone.m_DampingDistrib.keys.Length > 0)
						p.m_Damping *= bone.m_DampingDistrib.Evaluate(a);
					if (bone.m_ElasticityDistrib != null && bone.m_ElasticityDistrib.keys.Length > 0)
						p.m_Elasticity *= bone.m_ElasticityDistrib.Evaluate(a);
					if (bone.m_StiffnessDistrib != null && bone.m_StiffnessDistrib.keys.Length > 0)
						p.m_Stiffness *= bone.m_StiffnessDistrib.Evaluate(a);
					if (bone.m_InertDistrib != null && bone.m_InertDistrib.keys.Length > 0)
						p.m_Inert *= bone.m_InertDistrib.Evaluate(a);
					if (bone.m_RadiusDistrib != null && bone.m_RadiusDistrib.keys.Length > 0)
						p.m_Radius *= bone.m_RadiusDistrib.Evaluate(a);
				}

				p.m_Damping = Mathf.Clamp01(p.m_Damping);
				p.m_Elasticity = Mathf.Clamp01(p.m_Elasticity);
				p.m_Stiffness = Mathf.Clamp01(p.m_Stiffness);
				p.m_Inert = Mathf.Clamp01(p.m_Inert);
				p.m_Radius = Mathf.Max(p.m_Radius, 0);
			}
		}
	}
}
