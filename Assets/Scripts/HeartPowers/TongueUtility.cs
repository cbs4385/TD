using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.HeartPowers
{
    /// <summary>
    /// Holds the bone data for a single tongue instance.
    /// Used by both HeartOfTheMaze and HeartwardGraspEffect to avoid duplicating
    /// bone discovery, length calculation, and rotation logic.
    /// </summary>
    public class TongueBoneData
    {
        public Transform[] Bones;
        public Vector3[] RestPositions;
        public Quaternion[] RestRotations;
        public float[] RestWorldZOffsets;  // Each bone's world Z relative to tongue instance Z in rest pose
        public SkinnedMeshRenderer Renderer;
        public float Length;
    }

    /// <summary>
    /// Shared tongue bone manipulation utilities used by HeartOfTheMaze and HeartwardGraspEffect.
    /// Consolidates bone discovery, length calculation, rest pose storage, and rotation logic
    /// that was previously duplicated across both systems.
    /// </summary>
    public static class TongueUtility
    {
        // Shared tongue geometry constants
        public const float TONGUE_START_Z = 28.0f;
        public const float TONGUE_GROUND_Z = 0.0f;
        public const float TONGUE_HIDDEN_Z = 1000f;
        public const int BEND_BONE_COUNT = 5;

        /// <summary>
        /// Extracts the bone number from a bone name like "Bone_000", "Bone.001", "Bone123", etc.
        /// </summary>
        public static int ExtractBoneNumber(string boneName)
        {
            string digits = "";
            foreach (char c in boneName)
            {
                if (char.IsDigit(c))
                {
                    digits += c;
                }
            }

            if (string.IsNullOrEmpty(digits))
            {
                return 0;
            }

            if (int.TryParse(digits, out int result))
            {
                return result;
            }

            return 0;
        }

        /// <summary>
        /// Discovers bones from a tongue instance, sorts them by number, stores rest poses,
        /// and calculates tongue length. Returns a fully populated TongueBoneData.
        /// </summary>
        public static TongueBoneData SetupTongueBones(GameObject tongueInstance)
        {
            var data = new TongueBoneData();
            if (tongueInstance == null) return data;

            data.Renderer = tongueInstance.GetComponentInChildren<SkinnedMeshRenderer>();

            // Get bones from hierarchy, filtering to only Bone_* transforms.
            // Excludes BoneCollider_N, SolidCollider_N, TipTrigger, etc. which are
            // baked physics objects parented to bones. Without filtering, these
            // interleave with real bones and their 10× deeper Z offsets cause
            // FindGroundBoneIndex to jump erratically.
            {
                var boneList = new List<Transform>();
                foreach (var t in tongueInstance.GetComponentsInChildren<Transform>())
                {
                    if (t.name.StartsWith("Bone_"))
                    {
                        boneList.Add(t);
                    }
                }
                boneList.Sort((a, b) => ExtractBoneNumber(a.name).CompareTo(ExtractBoneNumber(b.name)));
                data.Bones = boneList.ToArray();
            }

            // Store rest poses
            if (data.Bones != null && data.Bones.Length > 0)
            {
                data.RestPositions = new Vector3[data.Bones.Length];
                data.RestRotations = new Quaternion[data.Bones.Length];

                for (int i = 0; i < data.Bones.Length; i++)
                {
                    if (data.Bones[i] != null)
                    {
                        data.RestPositions[i] = data.Bones[i].localPosition;
                        data.RestRotations[i] = data.Bones[i].localRotation;
                    }
                }

                data.Length = CalculateTongueLength(data.Bones);
            }

            return data;
        }

        /// <summary>
        /// Calculates total tongue length from first to last bone plus one segment.
        /// </summary>
        public static float CalculateTongueLength(Transform[] bones)
        {
            if (bones == null || bones.Length < 2) return 0f;

            Vector3 firstBone = bones[0].position;
            Vector3 lastBone = bones[bones.Length - 1].position;
            float length = Vector3.Distance(firstBone, lastBone);

            // Add one more bone segment for the tip
            if (bones.Length > 1)
            {
                length += length / (bones.Length - 1);
            }

            return length;
        }

        /// <summary>
        /// Resets all bones to their stored rest positions and rotations.
        /// </summary>
        public static void ResetBonesToRest(TongueBoneData data)
        {
            if (data == null || data.Bones == null || data.RestPositions == null || data.RestRotations == null) return;

            for (int i = 0; i < data.Bones.Length; i++)
            {
                if (data.Bones[i] != null)
                {
                    data.Bones[i].localPosition = data.RestPositions[i];
                    data.Bones[i].localRotation = data.RestRotations[i];
                }
            }
        }

        /// <summary>
        /// Caches each bone's world Z offset relative to the tongue instance's world Z.
        /// Must be called once after SetupTongueBones, with bones in rest pose.
        /// After caching, FindGroundBoneIndex uses these offsets instead of reading
        /// potentially stale bone world positions after ResetBonesToRest.
        /// </summary>
        public static void CacheRestWorldZOffsets(TongueBoneData data, GameObject tongueInstance)
        {
            if (data?.Bones == null || tongueInstance == null) return;

            ResetBonesToRest(data);

            float instanceWorldZ = tongueInstance.transform.position.z;
            data.RestWorldZOffsets = new float[data.Bones.Length];

            for (int i = 0; i < data.Bones.Length; i++)
            {
                if (data.Bones[i] != null)
                {
                    data.RestWorldZOffsets[i] = data.Bones[i].position.z - instanceWorldZ;
                }
            }

        }

        /// <summary>
        /// Applies bone rotations for tongue extending horizontally toward a target.
        /// All bones are already at rest pose (set by FindGroundBoneIndex's ResetBonesToRest call).
        /// Bones below lipBoneIndex stay at rest (vertical column underground).
        /// Bones from lipBoneIndex to lipBoneIndex+BEND_BONE_COUNT interpolate from vertical to horizontal.
        /// Bones past the bend zone point fully horizontal toward the target.
        /// </summary>
        /// <param name="data">Tongue bone data with bones and rest poses</param>
        /// <param name="lipBoneIndex">Index of the bone at ground level (start of bend)</param>
        /// <param name="targetAngle">Angle in degrees toward target in the XY plane</param>
        public static void ApplyBoneRotations(TongueBoneData data, int lipBoneIndex, float targetAngle)
        {
            if (data == null || data.Bones == null || data.Bones.Length == 0) return;
            if (data.RestPositions == null || data.RestRotations == null) return;

            int boneCount = data.Bones.Length;
            int bendEndIndex = Mathf.Min(lipBoneIndex + BEND_BONE_COUNT, boneCount - 1);

            // Direction toward target (horizontal in XY plane)
            Vector3 targetDirWorld = new Vector3(
                Mathf.Cos(targetAngle * Mathf.Deg2Rad),
                Mathf.Sin(targetAngle * Mathf.Deg2Rad),
                0f
            );

            // Bones below lip are already at rest (FindGroundBoneIndex calls ResetBonesToRest).
            // Apply rotations from lip bone to tip.
            for (int i = lipBoneIndex; i < boneCount; i++)
            {
                if (data.Bones[i] == null) continue;
                data.Bones[i].localPosition = data.RestPositions[i];

                Quaternion parentWorldRot = data.Bones[i].parent != null ?
                    data.Bones[i].parent.rotation : Quaternion.identity;

                // Bone forward direction (local +Y points toward next bone)
                Vector3 boneLocalDir = Vector3.up;
                Vector3 boneWorldDir = parentWorldRot * data.RestRotations[i] * boneLocalDir;

                Vector3 desiredDir;
                if (i >= lipBoneIndex && i <= bendEndIndex)
                {
                    // Bend zone: interpolate from vertical (Vector3.back = -Z = up in game) to horizontal
                    float bendT = (float)(i - lipBoneIndex) / (float)BEND_BONE_COUNT;
                    bendT = Mathf.Clamp01(bendT);
                    desiredDir = Vector3.Slerp(Vector3.back, targetDirWorld, bendT);
                }
                else
                {
                    // Past bend zone: fully horizontal toward target
                    desiredDir = targetDirWorld;
                }

                if (desiredDir.sqrMagnitude > 0.0001f && boneWorldDir.sqrMagnitude > 0.0001f)
                {
                    desiredDir = desiredDir.normalized;
                    boneWorldDir = boneWorldDir.normalized;

                    float dot = Vector3.Dot(boneWorldDir, desiredDir);
                    Quaternion worldCorrection;

                    if (dot > 0.9999f)
                    {
                        worldCorrection = Quaternion.identity;
                    }
                    else if (dot < -0.9999f)
                    {
                        Vector3 perpAxis = Vector3.Cross(boneWorldDir, Vector3.up);
                        if (perpAxis.sqrMagnitude < 0.0001f)
                            perpAxis = Vector3.Cross(boneWorldDir, Vector3.right);
                        perpAxis.Normalize();
                        worldCorrection = Quaternion.AngleAxis(180f, perpAxis);
                    }
                    else
                    {
                        worldCorrection = Quaternion.FromToRotation(boneWorldDir, desiredDir);
                    }

                    Quaternion newLocalRot = Quaternion.Inverse(parentWorldRot) * worldCorrection * parentWorldRot * data.RestRotations[i];

                    if (!float.IsNaN(newLocalRot.x) && !float.IsNaN(newLocalRot.y) &&
                        !float.IsNaN(newLocalRot.z) && !float.IsNaN(newLocalRot.w))
                    {
                        data.Bones[i].localRotation = newLocalRot;
                    }
                }
            }
        }

        /// <summary>
        /// Finds the first bone whose rest-pose world Z position is at or above groundZ.
        /// Returns -1 if no bone has reached groundZ yet.
        ///
        /// Uses cached RestWorldZOffsets (computed at setup time) instead of reading bone
        /// world positions directly. Reading bone.position.z after ResetBonesToRest can
        /// return stale values because Unity may not fully propagate world transforms
        /// through the bone hierarchy within the same frame.
        ///
        /// Still resets bones to rest pose so ApplyBoneRotations (called after this)
        /// starts from a clean state.
        /// </summary>
        /// <param name="data">Tongue bone data with cached rest Z offsets</param>
        /// <param name="groundZ">World Z at which the bend should occur (e.g. -0.25)</param>
        /// <param name="tongueInstance">The tongue GameObject whose world Z is used as reference</param>
        public static int FindGroundBoneIndex(TongueBoneData data, float groundZ, GameObject tongueInstance)
        {
            if (data?.Bones == null || data.RestWorldZOffsets == null || tongueInstance == null) return -1;

            // Reset bones to rest pose for the subsequent ApplyBoneRotations call
            ResetBonesToRest(data);

            float instanceWorldZ = tongueInstance.transform.position.z;
            int boneCount = data.Bones.Length;

            for (int i = 0; i < boneCount; i++)
            {
                if (data.Bones[i] == null) continue;
                float expectedWorldZ = instanceWorldZ + data.RestWorldZOffsets[i];
                if (expectedWorldZ <= groundZ)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Captures the visitor's position and heading angle at the moment of grab.
        /// Position offset keeps visitor at their current position relative to tip.
        /// Heading is preserved throughout grab/retract while the visitor tilts with the tongue curve.
        /// </summary>
        /// <param name="data">Tongue bone data</param>
        /// <param name="visitor">The visitor transform being grabbed</param>
        /// <param name="positionOffset">Output: visitor position relative to tip bone</param>
        /// <param name="visitorHeadingAngle">Output: visitor's Z rotation (heading in XY plane, degrees)</param>
        /// <param name="chainAngleAtGrab">Output: not used, kept for signature compatibility</param>
        public static void CaptureGrabOffsets(TongueBoneData data, Transform visitor,
            out Vector3 positionOffset, out float visitorHeadingAngle, out float chainAngleAtGrab)
        {
            positionOffset = Vector3.zero;
            visitorHeadingAngle = 0f;
            chainAngleAtGrab = 0f;
            if (data?.Bones == null || data.Bones.Length == 0 || visitor == null) return;

            int tipIndex = data.Bones.Length - 1;
            Transform tipBone = data.Bones[tipIndex];
            if (tipBone == null) return;

            // Capture visitor's Z rotation directly (their walking heading)
            // This is the angle they face in the XY plane
            visitorHeadingAngle = visitor.eulerAngles.z;

            // Capture position offset relative to tip bone
            // This keeps visitor at their current position, not jumping to tip bone
            positionOffset = visitor.position - tipBone.position;

            // chainAngleAtGrab not used in current approach
            chainAngleAtGrab = 0f;
        }

        /// <summary>
        /// Moves a visitor to the tongue tip bone plus offset.
        /// Visitor heading (XY facing direction) is preserved from grab time.
        /// Visitor tilts with the tongue curve - when tongue is horizontal they're flat,
        /// when tongue is vertical they're tilted upright.
        ///
        /// Position offset interpretation:
        /// - For grabbing tongues: world-space offset captured at grab time
        /// - For pushing tongue: chain-relative offset (X component = distance along chain backward from tip)
        /// </summary>
        /// <param name="chainRelativeOffset">If true, positionOffset.x is treated as distance backward along chain</param>
        public static void MoveVisitorToTip(TongueBoneData data, Transform visitor,
            Vector3 positionOffset, float visitorHeadingAngle, float chainAngleAtGrab,
            bool chainRelativeOffset = false)
        {
            if (data?.Bones == null || data.Bones.Length == 0 || visitor == null) return;

            int tipIndex = data.Bones.Length - 1;
            Transform tipBone = data.Bones[tipIndex];
            if (tipBone == null) return;

            // Get chain direction from lookback bone to tip
            int lookbackIndex = Mathf.Max(0, tipIndex - 10);
            Transform lookbackBone = data.Bones[lookbackIndex];

            Vector3 chainDir = Vector3.forward;
            if (lookbackBone != null)
            {
                chainDir = (tipBone.position - lookbackBone.position).normalized;
            }

            // Calculate position
            if (chainRelativeOffset)
            {
                // positionOffset.x = distance forward along chain (for pushing tongue)
                // positionOffset.y = perpendicular offset toward -Z (to center model midpoint on tip)
                // Positive chain direction = forward from tip in direction of travel

                // Calculate perpendicular direction (toward -Z / world up, perpendicular to chain)
                // Cross product of chain direction with a horizontal vector gives the perpendicular
                Vector3 horizontalPerp = new Vector3(-chainDir.y, chainDir.x, 0f).normalized;
                Vector3 upPerp = Vector3.Cross(chainDir, horizontalPerp).normalized;

                // If chain is pointing into ground (+Z), upPerp should point toward -Z
                // If chain is horizontal, upPerp should point toward -Z
                // Ensure upPerp has negative Z component (pointing "up" in world)
                if (upPerp.z > 0) upPerp = -upPerp;

                visitor.position = tipBone.position + chainDir * positionOffset.x + upPerp * positionOffset.y;
            }
            else
            {
                // World-space offset (for grabbing tongues)
                visitor.position = tipBone.position + positionOffset;
            }

            if (lookbackBone == null)
            {
                // Fallback: just use original heading, no tilt
                visitor.rotation = Quaternion.Euler(0f, 0f, visitorHeadingAngle);
                return;
            }

            // Calculate tilt angle from chain direction
            // tiltAngle = how much chain deviates from horizontal (XY plane)
            // horizontal → 0°, pointing +Z (down into ground) → +90°, pointing -Z (up) → -90°
            float horizontalMag = Mathf.Sqrt(chainDir.x * chainDir.x + chainDir.y * chainDir.y);
            float tiltAngle = Mathf.Atan2(chainDir.z, horizontalMag) * Mathf.Rad2Deg;

            // Original heading rotation (flat in XY plane)
            Quaternion headingRot = Quaternion.Euler(0f, 0f, visitorHeadingAngle);

            // Tilt axis: perpendicular to heading direction in XY plane
            // If heading h makes visitor face (cos(h), sin(h), 0), tilt axis is (-sin(h), cos(h), 0)
            float h = visitorHeadingAngle * Mathf.Deg2Rad;
            Vector3 tiltAxis = new Vector3(-Mathf.Sin(h), Mathf.Cos(h), 0f);

            // Tilt rotation around that axis
            Quaternion tiltRot = Quaternion.AngleAxis(tiltAngle, tiltAxis);

            // Apply: first heading (flat), then tilt around perpendicular axis
            visitor.rotation = tiltRot * headingRot;
        }
    }
}
