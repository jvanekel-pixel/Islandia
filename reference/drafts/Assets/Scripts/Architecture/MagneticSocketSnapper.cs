using System;
using UnityEngine;

namespace IslandBuilder.Architecture
{
    public enum SocketDirection : byte
    {
        North  = 0, // +Z
        South  = 1, // -Z
        East   = 2, // +X
        West   = 3, // -X
        Top    = 4, // +Y
        Bottom = 5  // -Y
    }

    [Serializable]
    public structLocalSocketPoint
    {
        public SocketDirection Direction;
        public Vector3 LocalOffset;
        public Vector3 LocalForward;
    }

    public sealed class MagneticSocketSnapper : MonoBehaviour
    {
        [Header("Module Metadata")]
        [SerializeField] private int catalogId;
        [SerializeField] private bool canBeWelded = true;
        [SerializeField] private structLocalSocketPoint[] localSockets =
        {
            new structLocalSocketPoint { Direction = SocketDirection.North,  LocalOffset = new Vector3(0f, 0f, 1.0f),  LocalForward = Vector3.forward },
            new structLocalSocketPoint { Direction = SocketDirection.South,  LocalOffset = new Vector3(0f, 0f, -1.0f), LocalForward = Vector3.back },
            new structLocalSocketPoint { Direction = SocketDirection.East,   LocalOffset = new Vector3(1.0f, 0f, 0f),  LocalForward = Vector3.right },
            new structLocalSocketPoint { Direction = SocketDirection.West,   LocalOffset = new Vector3(-1.0f, 0f, 0f), LocalForward = Vector3.left },
            new structLocalSocketPoint { Direction = SocketDirection.Top,    LocalOffset = new Vector3(0f, 3.0f, 0f),  LocalForward = Vector3.up },
            new structLocalSocketPoint { Direction = SocketDirection.Bottom, LocalOffset = new Vector3(0f, 0f, 0f),    LocalForward = Vector3.down }
        };

        private static readonly Collider[] OverlapCollidersScratch = new Collider[32];

        public int CatalogId
        {
            get => catalogId;
            set => catalogId = value;
        }

        public bool CanBeWelded
        {
            get => canBeWelded;
            set => canBeWelded = value;
        }

        public structLocalSocketPoint[] LocalSockets => localSockets;

        /// <summary>
        /// Evaluates nearby placed modular building pieces and magnetically snaps the candidate world position
        /// and rotation to the closest complementary socket within snapRadiusMeters (Zero GC allocation).
        /// </summary>
        public bool TryComputeMagneticSnap(
            Vector3 candidateWorldPos,
            Quaternion candidateWorldRot,
            float snapRadiusMeters,
            LayerMask buildingLayerMask,
            out Vector3 snappedWorldPos,
            out Quaternion snappedWorldRot)
        {
            snappedWorldPos = candidateWorldPos;
            snappedWorldRot = candidateWorldRot;

            int colliderCount = Physics.OverlapSphereNonAlloc(
                candidateWorldPos,
                snapRadiusMeters * 3.5f,
                OverlapCollidersScratch,
                buildingLayerMask,
                QueryTriggerInteraction.Ignore);

            if (colliderCount == 0 || localSockets == null || localSockets.Length == 0)
            {
                return false;
            }

            float bestDistSq = snapRadiusMeters * snapRadiusMeters;
            bool foundSnap = false;

            for (int c = 0; c < colliderCount; c++)
            {
                Collider col = OverlapCollidersScratch[c];
                if (col == null || col.transform == transform || !col.gameObject.activeInHierarchy)
                {
                    continue;
                }

                MagneticSocketSnapper targetModule = col.GetComponentInParent<MagneticSocketSnapper>();
                if (targetModule == null || targetModule == this) continue;

                var targetSockets = targetModule.localSockets;
                if (targetSockets == null) continue;

                Transform targetTransform = targetModule.transform;

                for (int ts = 0; ts < targetSockets.Length; ts++)
                {
                    structLocalSocketPoint targetSock = targetSockets[ts];
                    SocketDirection requiredMyDir = GetOppositeDirection(targetSock.Direction);
                    Vector3 targetSocketWorldPos = targetTransform.TransformPoint(targetSock.LocalOffset);

                    for (int ms = 0; ms < localSockets.Length; ms++)
                    {
                        structLocalSocketPoint mySock = localSockets[ms];
                        if (mySock.Direction != requiredMyDir) continue;

                        Vector3 mySocketWorldOffset = candidateWorldRot * mySock.LocalOffset;
                        Vector3 mySocketCandidatePos = candidateWorldPos + mySocketWorldOffset;

                        float distSq = (targetSocketWorldPos - mySocketCandidatePos).sqrMagnitude;
                        if (distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            snappedWorldPos = targetSocketWorldPos - mySocketWorldOffset;
                            snappedWorldRot = Quaternion.Euler(0f, Mathf.Round(candidateWorldRot.eulerAngles.y / 90f) * 90f, 0f);
                            foundSnap = true;
                        }
                    }
                }
            }

            return foundSnap;
        }

        private static SocketDirection GetOppositeDirection(SocketDirection dir)
        {
            return dir switch
            {
                SocketDirection.North  => SocketDirection.South,
                SocketDirection.South  => SocketDirection.North,
                SocketDirection.East   => SocketDirection.West,
                SocketDirection.West   => SocketDirection.East,
                SocketDirection.Top    => SocketDirection.Bottom,
                SocketDirection.Bottom => SocketDirection.Top,
                _ => SocketDirection.South
            };
        }
    }
}
