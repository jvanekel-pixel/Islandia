using System;
using IslandBuilder.TerrainSystem;
using UnityEngine;

namespace IslandBuilder.Pets
{
    public sealed class IslandPetController : MonoBehaviour
    {
        [Header("Skinned Mesh & Bone References (<= 20 Bones)")]
        [SerializeField] private SkinnedMeshRenderer primarySkinnedRenderer;
        [SerializeField] private Transform rootBone;
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform tailBone;
        [SerializeField] private Transform frontLeftLegBone;
        [SerializeField] private Transform frontRightLegBone;
        [SerializeField] private Transform backLeftLegBone;
        [SerializeField] private Transform backRightLegBone;

        [Header("Customization State")]
        [SerializeField] private PetCustomizationProfile currentProfile;

        private static readonly int PrimaryColorShaderId = Shader.PropertyToID("_PetPrimaryColor");
        private static readonly int SecondaryColorShaderId = Shader.PropertyToID("_PetSecondaryColor");
        private static readonly int AccentColorShaderId = Shader.PropertyToID("_PetAccentColor");
        private static readonly int PatternParamsShaderId = Shader.PropertyToID("_PetPatternParams");
        private static readonly int CollarTagTexShaderId = Shader.PropertyToID("_CollarTagTex");

        private MaterialPropertyBlock _coatPropBlock;
        private Texture2D _bakedCollarTagTexture;
        private float _locomotionPhase;
        private float _currentSpeedNormalized;
        private bool _isSunbathingOrSleeping;
        private Vector3 _lookTargetWorldPos;
        private bool _hasLookTarget;

        public PetCustomizationProfile CurrentProfile => currentProfile;
        public SkinnedMeshRenderer PrimarySkinnedRenderer => primarySkinnedRenderer;

        public float CurrentSpeedNormalized
        {
            get => _currentSpeedNormalized;
            set => _currentSpeedNormalized = Mathf.Clamp01(value);
        }

        public bool IsSunbathingOrSleeping
        {
            get => _isSunbathingOrSleeping;
            set => _isSunbathingOrSleeping = value;
        }

        private void Awake()
        {
            _coatPropBlock = new MaterialPropertyBlock();

            if (primarySkinnedRenderer == null)
            {
                primarySkinnedRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            }

            // CRITICAL PHASE 10 OPTIMIZATION:
            // Disable off-screen compute skinning so pets behind the player cost 0.0 ms!
            if (primarySkinnedRenderer != null)
            {
                primarySkinnedRenderer.updateWhenOffscreen = false;
                primarySkinnedRenderer.quality = SkinQuality.Bone2;
            }

            Animator anim = GetComponent<Animator>();
            if (anim != null)
            {
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
        }

        private void OnDestroy()
        {
            if (_bakedCollarTagTexture != null)
            {
                Destroy(_bakedCollarTagTexture);
            }
        }

        /// <summary>
        /// Applies BlendShape morphology sliders (Ear Droop, Snout Length, Tail Fluff, Body Chubbiness),
        /// RGBA procedural coat colors/patterns, and bakes the collar nameplate texture.
        /// </summary>
        public void ApplyCustomizationProfile(PetCustomizationProfile profile)
        {
            currentProfile = profile;
            if (primarySkinnedRenderer == null) return;

            // 1. Apply 4 Procedural BlendShapes (Evaluated on GPU via Vulkan Compute Skinning)
            Mesh sharedMesh = primarySkinnedRenderer.sharedMesh;
            if (sharedMesh != null)
            {
                int count = sharedMesh.blendShapeCount;
                if (count > 0) primarySkinnedRenderer.SetBlendShapeWeight(0, profile.EarDroopOrCrestWeight);
                if (count > 1) primarySkinnedRenderer.SetBlendShapeWeight(1, profile.SnoutLengthWeight);
                if (count > 2) primarySkinnedRenderer.SetBlendShapeWeight(2, profile.TailFluffOrWingWeight);
                if (count > 3) primarySkinnedRenderer.SetBlendShapeWeight(3, profile.BodyChubbinessWeight);
            }

            // 2. Bake Collar Nameplate Texture once (avoids floating UI Canvas overdraw)
            BakeCollarNameplateBadge(profile.CustomPetName);

            // 3. Push Coat Colors & Spot/Stripe Pattern via MaterialPropertyBlock (Preserves Batching)
            primarySkinnedRenderer.GetPropertyBlock(_coatPropBlock);
            _coatPropBlock.SetColor(PrimaryColorShaderId, profile.PrimaryCoatColor);
            _coatPropBlock.SetColor(SecondaryColorShaderId, profile.SecondaryBellyColor);
            _coatPropBlock.SetColor(AccentColorShaderId, profile.AccentSpotSockColor);
            _coatPropBlock.SetVector(PatternParamsShaderId, new Vector4(
                (float)profile.PatternStyle,
                Mathf.Max(2.0f, profile.PatternFrequency),
                0.35f,
                0.15f));

            if (_bakedCollarTagTexture != null)
            {
                _coatPropBlock.SetTexture(CollarTagTexShaderId, _bakedCollarTagTexture);
            }

            primarySkinnedRenderer.SetPropertyBlock(_coatPropBlock);
        }

        public void SetHeadLookTarget(Vector3 worldPos, bool active)
        {
            _lookTargetWorldPos = worldPos;
            _hasLookTarget = active;
        }

        private void LateUpdate()
        {
            // CullUpdateTransforms equivalent for procedural bone gait:
            // Do not update bone transforms if the pet's bounding box is outside the VR camera frustum!
            if (primarySkinnedRenderer != null && !primarySkinnedRenderer.isVisible)
            {
                return;
            }

            EvaluateLightweightProceduralBoneAnimation();
        }

        private void EvaluateLightweightProceduralBoneAnimation()
        {
            float dt = Time.deltaTime;
            float wagFreq = _isSunbathingOrSleeping ? 2.0f : Mathf.Lerp(5.5f, 14.0f, _currentSpeedNormalized);
            _locomotionPhase += dt * wagFreq;

            // 1. Happy Tail Wag / Avian Tail Bob
            if (tailBone != null)
            {
                float wagAngle = Mathf.Sin(_locomotionPhase * 1.4f) * (_isSunbathingOrSleeping ? 8.0f : 28.0f);
                tailBone.localRotation = Quaternion.Euler(25.0f, wagAngle, 0.0f);
            }

            // 2. Quadrupedal / Avian Leg Swing Cycle
            float swingAmp = _isSunbathingOrSleeping ? 0.0f : (_currentSpeedNormalized * 34.0f);
            float sinPhase = Mathf.Sin(_locomotionPhase);

            if (frontLeftLegBone != null) frontLeftLegBone.localRotation = Quaternion.Euler(sinPhase * swingAmp, 0f, 0f);
            if (backRightLegBone != null) backRightLegBone.localRotation = Quaternion.Euler(sinPhase * swingAmp, 0f, 0f);
            if (frontRightLegBone != null) frontRightLegBone.localRotation = Quaternion.Euler(-sinPhase * swingAmp, 0f, 0f);
            if (backLeftLegBone != null) backLeftLegBone.localRotation = Quaternion.Euler(-sinPhase * swingAmp, 0f, 0f);

            // 3. Root Sunbathing Pose or Gentle Breathing Bob
            if (rootBone != null)
            {
                float baseHeight = _isSunbathingOrSleeping ? 0.14f : 0.28f;
                float bounce = Mathf.Abs(sinPhase) * (_currentSpeedNormalized * 0.04f);
                rootBone.localPosition = new Vector3(0f, baseHeight + bounce, 0f);
            }

            // 4. Curious Head Tracking toward Player, Companion Pet, or Fetch Toy
            if (headBone != null)
            {
                if (_hasLookTarget)
                {
                    Vector3 toTarget = _lookTargetWorldPos - headBone.position;
                    if (toTarget.sqrMagnitude > 0.05f)
                    {
                        Quaternion targetWorldRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                        headBone.rotation = Quaternion.Slerp(headBone.rotation, targetWorldRot, dt * 6.0f);
                    }
                }
                else
                {
                    float headTilt = Mathf.Sin(_locomotionPhase * 0.5f) * 6.0f;
                    headBone.localRotation = Quaternion.Slerp(headBone.localRotation, Quaternion.Euler(0f, 0f, headTilt), dt * 5.0f);
                }
            }
        }

        /// <summary>
        /// Generates a crisp 64x16 metallic gold collar badge texture encoded with the pet's name hash
        /// so each pet wears a personalized collar with zero UI Canvas draw calls.
        /// </summary>
        private void BakeCollarNameplateBadge(string petName)
        {
            if (_bakedCollarTagTexture == null)
            {
                _bakedCollarTagTexture = new Texture2D(64, 16, TextureFormat.RGBA32, false, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            int hash = string.IsNullOrEmpty(petName) ? 7 : petName.GetHashCode();
            Color strapCol = new Color(0.78f, 0.16f, 0.18f); // Crimson leather collar strap
            Color brassMedal = new Color(0.96f, 0.82f, 0.28f); // Engraved gold nameplate tag

            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    bool isMedalZone = x >= 20 && x <= 44 && y >= 3 && y <= 12;
                    if (!isMedalZone)
                    {
                        _bakedCollarTagTexture.SetPixel(x, y, strapCol);
                    }
                    else
                    {
                        // Deterministic glyph pattern representing the engraved pet name on the brass tag
                        bool isGlyphPixel = (y >= 6 && y <= 9) && (((hash >> ((x - 20) % 16)) & 1) != 0);
                        _bakedCollarTagTexture.SetPixel(x, y, isGlyphPixel ? new Color(0.25f, 0.18f, 0.05f) : brassMedal);
                    }
                }
            }

            _bakedCollarTagTexture.Apply(false, false);
        }
    }
}
