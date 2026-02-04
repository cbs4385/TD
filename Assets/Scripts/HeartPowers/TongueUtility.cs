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

            if (data.Renderer != null && data.Renderer.bones != null && data.Renderer.bones.Length > 0)
            {
                data.Bones = data.Renderer.bones;
            }
            else
            {
                // Fallback: find bones by name and sort them by bone number
                var boneList = new List<Transform>();
                foreach (var t in tongueInstance.GetComponentsInChildren<Transform>())
                {
                    string nameLower = t.name.ToLower();
                    if (nameLower.Contains("bone") || nameLower.Contains("joint"))
                    {
                        boneList.Add(t);
                    }
                }

                // CRITICAL: Sort bones by their number (Bone_000, Bone_001, etc.)
                // Without sorting, bones may be in arbitrary order which breaks the bending logic
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
        /// Applies bone rotations for tongue extending horizontally at ground level.
        /// Bones from lipBoneIndex onward are rotated to bend 90° and extend toward targetAngle.
        /// Bones below lipBoneIndex stay at rest pose (pointing up).
        ///
        /// This is the canonical implementation with NaN checks and edge-case handling.
        /// </summary>
        /// <param name="data">Tongue bone data with bones and rest poses</param>
        /// <param name="lipBoneIndex">Index of the bone at ground level (the start of the bend)</param>
        /// <param name="targetAngle">Angle in degrees toward target in the XY plane</param>
        public static void ApplyBoneRotations(TongueBoneData data, int lipBoneIndex, float targetAngle)
        {
            if (data == null || data.Bones == null || data.Bones.Length == 0) return;
            if (data.RestPositions == null || data.RestRotations == null) return;

            int boneCount = data.Bones.Length;
            int bendEndIndex = Mathf.Min(lipBoneIndex + BEND_BONE_COUNT, boneCount - 1);

            // Direction toward target (horizontal)
            Vector3 targetDirWorld = new Vector3(
                Mathf.Cos(targetAngle * Mathf.Deg2Rad),
                Mathf.Sin(targetAngle * Mathf.Deg2Rad),
                0f
            );

            // Reset bones below lip to rest pose
            for (int i = 0; i < Mathf.Min(lipBoneIndex, boneCount); i++)
            {
                if (data.Bones[i] == null) continue;
                data.Bones[i].localPosition = data.RestPositions[i];
                data.Bones[i].localRotation = data.RestRotations[i];
            }

            // Apply rotations from lip bone to tip
            for (int i = lipBoneIndex; i < boneCount && lipBoneIndex >= 0; i++)
            {
                if (data.Bones[i] == null) continue;

                // Reset position (bones don't translate, only rotate)
                data.Bones[i].localPosition = data.RestPositions[i];

                Quaternion parentWorldRot = data.Bones[i].parent != null ?
                    data.Bones[i].parent.rotation : Quaternion.identity;

                // Bone forward direction (local +Y points toward next bone)
                Vector3 boneLocalDir = Vector3.up;
                Vector3 boneWorldDir = parentWorldRot * data.RestRotations[i] * boneLocalDir;

                Vector3 desiredDir;

                if (i >= lipBoneIndex && i <= bendEndIndex)
                {
                    // Bones in bend zone: interpolate from vertical (-Z is up) to horizontal
                    float bendT = (float)(i - lipBoneIndex) / (float)BEND_BONE_COUNT;
                    bendT = Mathf.Clamp01(bendT);
                    Vector3 upDir = Vector3.back;  // -Z is up
                    desiredDir = Vector3.Slerp(upDir, targetDirWorld, bendT);
                }
                else
                {
                    // Bones past bend zone: point horizontally toward target
                    desiredDir = targetDirWorld;
                }

                // Compute rotation to align bone toward desired direction
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

                    if (!float.IsNaN(newLocalRot.x) && !float.IsNaN(newLocalRot.y) && !float.IsNaN(newLocalRot.z) && !float.IsNaN(newLocalRot.w))
                    {
                        data.Bones[i].localRotation = newLocalRot;
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the ground bone index (lip bone) for a tongue at the given Z position.
        /// Returns -1 if no bone has emerged above ground yet.
        /// </summary>
        /// <param name="tongueZPosition">Current Z position of the tongue root</param>
        /// <param name="tongueLength">Total tongue length</param>
        /// <param name="boneCount">Number of bones</param>
        /// <param name="groundZ">Ground level Z (typically 0)</param>
        public static int FindGroundBoneIndex(float tongueZPosition, float tongueLength, int boneCount, float groundZ = TONGUE_GROUND_Z)
        {
            float boneSpacing = tongueLength / Mathf.Max(1, boneCount);

            for (int i = 0; i < boneCount; i++)
            {
                float boneZ = tongueZPosition - (i * boneSpacing);
                if (boneZ <= groundZ)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
