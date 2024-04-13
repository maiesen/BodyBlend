using System.Collections.Generic;
using UnityEngine;

namespace BodyBlend
{
	public enum DynBoneControlMode
	{
		ZERO_TO_BASE,
		FULL_CONTROL,
		BASE_TO_ONE
	}

	internal class DynamicBoneControl
	{

		private struct DynBoneValue
		{
			public float m_Damping;
			public float m_Elasticity;
			public float m_Stiffness;
			public float m_Inert;

			public DynBoneValue(float d, float e, float s, float i)
			{
				m_Damping = d;
				m_Elasticity = e;
				m_Stiffness = s;
				m_Inert = i;
			}
		}
		//private static readonly FieldInfo dynBoneParticlesField = typeof(DynamicBone).GetField("m_Particles", BindingFlags.NonPublic | BindingFlags.Instance);
		//private static readonly FieldInfo dynBoneTotalLengthField = typeof(DynamicBone).GetField("m_BoneTotalLength", BindingFlags.NonPublic | BindingFlags.Instance);

		private List<DynBoneValue> defaultBones = null;
		private List<DynamicBone> dynamicBones = null;

		// This is for controlling how inert the bone is as weight gets updated
		private AnimationCurve inertCurve = null;
		private AnimationCurve elasticityCurve = null;
		private AnimationCurve stiffnessCurve = null;
		private AnimationCurve dampingCurve = null;
		// Determine how bone parameters are adjusted from weight
		private DynBoneControlMode dynBoneControlMode = DynBoneControlMode.BASE_TO_ONE;

		internal void SetBoneControls(
			List<DynamicBone> bones,
			AnimationCurve inertCurve = null,
			AnimationCurve elasticityCurve = null,
			AnimationCurve stiffnessCurve = null,
			AnimationCurve dampingCurve = null)
		{
			if (bones == null || bones.Count <= 0)
				return;
			dynamicBones = bones;
			this.inertCurve = inertCurve;
			this.elasticityCurve = elasticityCurve;
			this.stiffnessCurve = stiffnessCurve;
			this.dampingCurve = dampingCurve;
			SaveDynBoneValues(dynamicBones);
		}

		internal void UpdateBoneValues(float currentWeight)
		{
			if (dynamicBones == null) return;
			for (int i = 0; i < dynamicBones.Count; i++)
			{
				dynamicBones[i].m_Inert = GetDynBoneValue(inertCurve, defaultBones[i].m_Inert, currentWeight);
				dynamicBones[i].m_Elasticity = GetDynBoneValue(elasticityCurve, defaultBones[i].m_Elasticity, currentWeight);
				dynamicBones[i].m_Stiffness = GetDynBoneValue(stiffnessCurve, defaultBones[i].m_Stiffness, currentWeight);
				dynamicBones[i].m_Damping = GetDynBoneValue(dampingCurve, defaultBones[i].m_Damping, currentWeight);

				UpdateDynBoneParameters(dynamicBones[i]);
			}
		}

		private float GetDynBoneValue(AnimationCurve curve, float defaultValue, float currentWeight)
		{
			if (curve == null)
			{
				return defaultValue;
			}

			var lerpVal = Mathf.Clamp01(curve.Evaluate(currentWeight));

			switch (dynBoneControlMode)
			{
				case DynBoneControlMode.BASE_TO_ONE:
					return Mathf.Lerp(defaultValue, 1f, lerpVal);
				case DynBoneControlMode.ZERO_TO_BASE:
					return Mathf.Lerp(0f, defaultValue, lerpVal);
				case DynBoneControlMode.FULL_CONTROL:
					return lerpVal;
			}

			return defaultValue;
		}

		private static void UpdateDynBoneParameters(DynamicBone bone)
		{
			var particles = bone.m_Particles;

			for (int i = 0; i < particles.Count; ++i)
			{
				DynamicBone.Particle p = particles[i];
				p.m_Damping = bone.m_Damping;
				p.m_Elasticity = bone.m_Elasticity;
				p.m_Stiffness = bone.m_Stiffness;
				p.m_Inert = bone.m_Inert;
				p.m_Radius = bone.m_Radius;

				var length = bone.m_BoneTotalLength;

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

		private void SaveDynBoneValues(List<DynamicBone> dynamicBones)
		{
			if (defaultBones == null)
				defaultBones = new List<DynBoneValue>();

			foreach (DynamicBone bone in dynamicBones)
			{
				defaultBones.Add(new DynBoneValue(
					bone.m_Damping,
					bone.m_Elasticity,
					bone.m_Stiffness,
					bone.m_Inert
				));
			}
		}
	}
}
