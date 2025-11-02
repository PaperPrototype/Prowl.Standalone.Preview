// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;

using Prowl.Echo;
using Prowl.Vector;

namespace Prowl.Runtime;

/// <summary>
/// Represents a skeletal structure with bones, hierarchy, and bind poses.
/// This class handles all the heavy lifting for bone IDs, transforms, and hierarchy.
/// </summary>
public sealed class Skeleton : EngineObject, ISerializable
{
    public List<Bone> Bones { get; private set; } = [];

    private Dictionary<string, int> _boneNameToIndex = [];
    private Dictionary<string, Bone> _boneNameToBone = [];

    /// <summary>
    /// Represents a single bone in the skeleton
    /// </summary>
    public class Bone
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public int ParentID { get; set; } = -1; // -1 means root bone

        // Bind pose (default/rest pose) - this is the local transform
        public Float3 BindPosition { get; set; }
        public Quaternion BindRotation { get; set; }
        public Float3 BindScale { get; set; }

        // Offset matrix (inverse bind pose in world space)
        // This is used for skinning to transform from bind pose to bone space
        public Float4x4 OffsetMatrix { get; set; }

        public Bone(int id, string name)
        {
            ID = id;
            Name = name;
            BindPosition = Float3.Zero;
            BindRotation = Quaternion.Identity;
            BindScale = Float3.One;
            OffsetMatrix = Float4x4.Identity;
        }
    }

    public Skeleton()
    {
    }

    /// <summary>
    /// Adds a bone to the skeleton
    /// </summary>
    public void AddBone(Bone bone)
    {
        Bones.Add(bone);
        _boneNameToIndex[bone.Name] = bone.ID;
        _boneNameToBone[bone.Name] = bone;
    }

    /// <summary>
    /// Gets a bone by name
    /// </summary>
    public Bone? GetBone(string name)
    {
        if (_boneNameToBone.TryGetValue(name, out Bone? bone))
            return bone;
        return null;
    }

    /// <summary>
    /// Gets a bone by ID
    /// </summary>
    public Bone? GetBone(int id)
    {
        if (id >= 0 && id < Bones.Count)
            return Bones[id];
        return null;
    }

    /// <summary>
    /// Gets the bone index by name
    /// </summary>
    public int GetBoneIndex(string name)
    {
        if (_boneNameToIndex.TryGetValue(name, out int index))
            return index;
        return -1;
    }

    /// <summary>
    /// Gets the parent bone of a given bone
    /// </summary>
    public Bone? GetParent(Bone bone)
    {
        if (bone.ParentID >= 0 && bone.ParentID < Bones.Count)
            return Bones[bone.ParentID];
        return null;
    }

    /// <summary>
    /// Calculates world transforms for all bones from local transforms
    /// </summary>
    public void CalculateWorldTransforms(Float3[] localPositions, Quaternion[] localRotations, Float3[] localScales,
        out Float4x4[] worldTransforms)
    {
        if (localPositions.Length != Bones.Count || localRotations.Length != Bones.Count || localScales.Length != Bones.Count)
            throw new ArgumentException("Transform arrays must match bone count");

        worldTransforms = new Float4x4[Bones.Count];

        // Calculate world transforms for each bone
        for (int i = 0; i < Bones.Count; i++)
        {
            Bone bone = Bones[i];
            Float4x4 localMatrix = Float4x4.CreateTRS(localPositions[i], localRotations[i], localScales[i]);

            if (bone.ParentID >= 0)
            {
                // Child bone: multiply by parent's world transform
                worldTransforms[i] = worldTransforms[bone.ParentID] * localMatrix;
            }
            else
            {
                // Root bone: world transform equals local transform
                worldTransforms[i] = localMatrix;
            }
        }
    }

    /// <summary>
    /// Calculates final bone matrices for GPU skinning
    /// </summary>
    public Float4x4[] CalculateSkinningMatrices(Float4x4[] worldTransforms)
    {
        if (worldTransforms.Length != Bones.Count)
            throw new ArgumentException("World transforms must match bone count");

        Float4x4[] skinningMatrices = new Float4x4[Bones.Count];

        for (int i = 0; i < Bones.Count; i++)
        {
            // Final matrix = world transform * offset matrix
            // This transforms from bind pose to current pose in world space
            skinningMatrices[i] = worldTransforms[i] * Bones[i].OffsetMatrix;
        }

        return skinningMatrices;
    }

    public void Serialize(ref EchoObject value, SerializationContext ctx)
    {
        value.Add("Name", new EchoObject(Name));

        var boneList = EchoObject.NewList();
        foreach (Bone bone in Bones)
        {
            var boneProp = EchoObject.NewCompound();
            boneProp.Add("ID", new EchoObject(bone.ID));
            boneProp.Add("Name", new EchoObject(bone.Name));
            boneProp.Add("ParentID", new EchoObject(bone.ParentID));

            boneProp.Add("BindPosition", Serializer.Serialize(bone.BindPosition, ctx));
            boneProp.Add("BindRotation", Serializer.Serialize(bone.BindRotation, ctx));
            boneProp.Add("BindScale", Serializer.Serialize(bone.BindScale, ctx));
            boneProp.Add("OffsetMatrix", Serializer.Serialize(bone.OffsetMatrix, ctx));

            boneList.ListAdd(boneProp);
        }
        value.Add("Bones", boneList);
    }

    public void Deserialize(EchoObject value, SerializationContext ctx)
    {
        Name = value.Get("Name").StringValue;

        EchoObject? boneList = value.Get("Bones");
        foreach (EchoObject boneProp in boneList.List)
        {
            var bone = new Bone(
                boneProp.Get("ID").IntValue,
                boneProp.Get("Name").StringValue
            );

            bone.ParentID = boneProp.Get("ParentID").IntValue;
            bone.BindPosition = Serializer.Deserialize<Float3>(boneProp.Get("BindPosition"), ctx);
            bone.BindRotation = Serializer.Deserialize<Quaternion>(boneProp.Get("BindRotation"), ctx);
            bone.BindScale = Serializer.Deserialize<Float3>(boneProp.Get("BindScale"), ctx);
            bone.OffsetMatrix = Serializer.Deserialize<Float4x4>(boneProp.Get("OffsetMatrix"), ctx);

            AddBone(bone);
        }
    }
}
