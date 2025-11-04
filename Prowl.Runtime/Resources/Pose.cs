// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;

using Prowl.Vector;

namespace Prowl.Runtime;

/// <summary>
/// Represents a single skeletal pose with transforms for all bones.
/// A pose is a snapshot of the skeleton at a specific point in time.
/// </summary>
public class Pose
{
    public Float3[] LocalPositions { get; private set; }
    public Quaternion[] LocalRotations { get; private set; }
    public Float3[] LocalScales { get; private set; }

    public int BoneCount => LocalPositions.Length;

    public Pose(int boneCount)
    {
        LocalPositions = new Float3[boneCount];
        LocalRotations = new Quaternion[boneCount];
        LocalScales = new Float3[boneCount];

        // Initialize to identity transforms
        for (int i = 0; i < boneCount; i++)
        {
            LocalPositions[i] = Float3.Zero;
            LocalRotations[i] = Quaternion.Identity;
            LocalScales[i] = Float3.One;
        }
    }

    /// <summary>
    /// Sets the transform for a specific bone
    /// </summary>
    public void SetBoneTransform(int boneID, Float3 position, Quaternion rotation, Float3 scale)
    {
        if (boneID < 0 || boneID >= BoneCount)
            throw new ArgumentOutOfRangeException(nameof(boneID));

        LocalPositions[boneID] = position;
        LocalRotations[boneID] = rotation;
        LocalScales[boneID] = scale;
    }

    /// <summary>
    /// Gets the local transform for a specific bone
    /// </summary>
    public void GetBoneTransform(int boneID, out Float3 position, out Quaternion rotation, out Float3 scale)
    {
        if (boneID < 0 || boneID >= BoneCount)
            throw new ArgumentOutOfRangeException(nameof(boneID));

        position = LocalPositions[boneID];
        rotation = LocalRotations[boneID];
        scale = LocalScales[boneID];
    }

    /// <summary>
    /// Calculates the final bone matrices for GPU skinning using mesh-specific offset matrices
    /// </summary>
    /// <param name="skeleton">The skeleton to use</param>
    /// <param name="boneNames">Bone names from the mesh</param>
    /// <param name="meshOffsetMatrices">Offset matrices specific to this mesh</param>
    public Float4x4[] GetBoneMatrices(Skeleton skeleton, string[] boneNames, Float4x4[] meshOffsetMatrices)
    {
        if (skeleton.Bones.Count != BoneCount)
            throw new ArgumentException("Pose bone count does not match skeleton bone count");

        // Calculate world transforms from local transforms
        skeleton.CalculateWorldTransforms(LocalPositions, LocalRotations, LocalScales, out Float4x4[] worldTransforms);

        // Calculate final skinning matrices using mesh-specific offsets
        return skeleton.CalculateSkinningMatrices(worldTransforms, boneNames, meshOffsetMatrices);
    }

    /// <summary>
    /// Creates a pose from the skeleton's bind pose
    /// </summary>
    public static Pose CreateBindPose(Skeleton skeleton)
    {
        Pose pose = new(skeleton.Bones.Count);

        for (int i = 0; i < skeleton.Bones.Count; i++)
        {
            Skeleton.Bone bone = skeleton.Bones[i];
            pose.SetBoneTransform(i, bone.BindPosition, bone.BindRotation, bone.BindScale);
        }

        return pose;
    }

    /// <summary>
    /// Copies this pose to a new instance
    /// </summary>
    public Pose Clone()
    {
        Pose clone = new(BoneCount);
        Array.Copy(LocalPositions, clone.LocalPositions, BoneCount);
        Array.Copy(LocalRotations, clone.LocalRotations, BoneCount);
        Array.Copy(LocalScales, clone.LocalScales, BoneCount);
        return clone;
    }
}
