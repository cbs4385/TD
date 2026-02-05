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

            // Diagnostic: log setup context
            Debug.Log($"[TongueDiag] CacheRestWorldZOffsets: boneCount={data.Bones.Length} instanceWorldZ={instanceWorldZ:F4} " +
                $"instanceLocalZ={tongueInstance.transform.localPosition.z:F4} " +
                $"bone[0]={data.Bones[0]?.name} offset[0]={data.RestWorldZOffsets[0]:F4} " +
                $"bone[last]={data.Bones[data.Bones.Length - 1]?.name} offset[last]={data.RestWorldZOffsets[data.Bones.Length - 1]:F4}");
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
        /// Computes a rotation representing the actual travel direction at the tongue tip
        /// by looking at the direction from a nearby bone to the tip bone in world space.
        ///
        /// ApplyBoneRotations keeps tip bones pointing horizontal until the very end of
        /// retraction (when the tip enters the bend zone). The bone chain's world positions
        /// however DO follow the curve. This method derives the true curve direction from
        /// the bone positions, so a grabbed visitor follows the tongue's curvature smoothly
        /// rather than staying horizontal and then snapping to vertical.
        /// </summary>
        /// <param name="data">Tongue bone data</param>
        /// <param name="lookbackCount">Number of bones to look back from the tip (default 10)</param>
        /// <returns>A rotation facing along the chain direction at the tip</returns>
        public static Quaternion GetChainRotationAtTip(TongueBoneData data, int lookbackCount = 10)
        {
            if (data == null || data.Bones == null || data.Bones.Length == 0) return Quaternion.identity;

            int tipIndex = data.Bones.Length - 1;
            Transform tipBone = data.Bones[tipIndex];
            if (tipBone == null) return Quaternion.identity;

            int lookbackIndex = Mathf.Max(0, tipIndex - lookbackCount);
            Transform lookbackBone = data.Bones[lookbackIndex];
            if (lookbackBone == null) return tipBone.rotation;

            Vector3 chainDir = (tipBone.position - lookbackBone.position).normalized;
            if (chainDir.sqrMagnitude < 0.001f) return tipBone.rotation;

            return Quaternion.LookRotation(chainDir, tipBone.up);
        }
    }
}
