using System;
using IslandBuilder.Core;
using IslandBuilder.TerrainSystem;
using UnityEngine;

namespace IslandBuilder.AI
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class PetFetchableToy : MonoBehaviour
    {
        public static event Action<PetFetchableToy> OnToyThrownByPlayer;

        [Header("Fetch Toy Settings")]
        [SerializeField] private string toyDisplayName = "Island Fetch Ball";
        [SerializeField] private float minThrowVelocityToTriggerFetch = 1.8f;

        private Rigidbody _rb;
        private bool _isClaimedByPet;
        private float _lastThrowTimestamp;

        public string ToyDisplayName => toyDisplayName;
        public Rigidbody ToyRigidbody => _rb;

        public bool IsClaimedByPet
        {
            get => _isClaimedByPet;
            set => _isClaimedByPet = value;
        }

        public float LastThrowTimestamp => _lastThrowTimestamp;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = 0.25f;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        /// <summary>
        /// Call when the player releases the VR Grab trigger / Hand Pinch or clicks to toss the toy.
        /// </summary>
        public void ThrowToyWithVelocity(Vector3 releaseVelocity)
        {
            transform.SetParent(null, true);
            _rb.isKinematic = false;
            _rb.linearVelocity = releaseVelocity;
            _isClaimedByPet = false;
            _lastThrowTimestamp = Time.time;

            if (releaseVelocity.sqrMagnitude >= minThrowVelocityToTriggerFetch * minThrowVelocityToTriggerFetch)
            {
                OnToyThrownByPlayer?.Invoke(this);
            }
        }

        public void AttachToPetMouth(Transform petHeadBone)
        {
            _isClaimedByPet = true;
            _rb.isKinematic = true;
            transform.SetParent(petHeadBone, false);
            transform.localPosition = new Vector3(0f, -0.04f, 0.18f);
            transform.localRotation = Quaternion.identity;
        }

        public void DropAtPlayerFeet(Vector3 dropWorldPos)
        {
            transform.SetParent(null, true);
            if (IslandTerrainManager.Instance != null)
            {
                dropWorldPos.y = Mathf.Max(IslandConstants.WaterLevelY + 0.1f,
                    IslandTerrainManager.Instance.SampleHeightAtWorldPosition(dropWorldPos)) + 0.15f;
            }
            transform.position = dropWorldPos;
            _rb.isKinematic = false;
            _rb.linearVelocity = Vector3.zero;
            _isClaimedByPet = false;
        }
    }
}
