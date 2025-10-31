// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Linq;

using Prowl.Runtime.Rendering;
using Prowl.Runtime.Resources;
using Prowl.Vector;
using Prowl.Vector.Geometry;

namespace Prowl.Runtime;

public class ModelRenderer : MonoBehaviour
{
    public Model Model;
    public Color MainColor = Color.White;

    // Animation properties
    public AnimationClip CurrentAnimation;
    public bool PlayAutomatically = true;
    public bool Loop = true;
    public float AnimationSpeed = 10.0f;

    private float _animationTime = 0.0f;
    private bool _isPlaying = false;
    private Dictionary<string, ModelNodeTransform> _nodeTransforms = [];
    private Dictionary<string, int> _boneNameToIndex = [];

    private class ModelNodeTransform
    {
        public Float3 Position;
        public Quaternion Rotation;
        public Float3 Scale;
        public Float4x4 LocalMatrix;
        public Float4x4 WorldMatrix;
    }

    public override void OnEnable()
    {
        if (Model.IsValid())
        {
            // Build node transform cache
            BuildNodeTransformCache(Model.RootNode, Float4x4.Identity);

            // Auto-play first animation if requested
            if (PlayAutomatically && Model.Animations.Count > 0)
            {
                CurrentAnimation = Model.Animations[0];
                Play();
            }
        }
    }

    public override void Update()
    {
        // Update animation
        if (_isPlaying && CurrentAnimation.IsValid())
        {
            _animationTime += Time.DeltaTime * AnimationSpeed;

            if (_animationTime >= CurrentAnimation.Duration)
            {
                if (Loop)
                {
                    _animationTime %= CurrentAnimation.Duration;
                }
                else
                {
                    _animationTime = CurrentAnimation.Duration;
                    _isPlaying = false;
                }
            }

            // Evaluate animation and update node transforms
            EvaluateAnimation(CurrentAnimation, _animationTime);
        }

        // Render the model
        if (Model.IsValid())
        {
            RenderModelNode(Model.RootNode, Transform.LocalToWorldMatrix);
        }
    }

    public void Play(AnimationClip animation = null)
    {
        if (animation.IsValid())
            CurrentAnimation = animation;

        if (CurrentAnimation.IsValid())
        {
            _animationTime = 0.0f;
            _isPlaying = true;
        }
    }

    public void Stop()
    {
        _isPlaying = false;
        _animationTime = 0.0f;
    }

    public void Pause()
    {
        _isPlaying = false;
    }

    public void Resume()
    {
        if (CurrentAnimation.IsValid())
            _isPlaying = true;
    }

    private void BuildNodeTransformCache(ModelNode node, Float4x4 parentWorldMatrix)
    {
        // Calculate this node's matrices
        var localMatrix = Float4x4.CreateTRS(node.LocalPosition, node.LocalRotation, node.LocalScale);
        Float4x4 worldMatrix = parentWorldMatrix * localMatrix;

        _nodeTransforms[node.Name] = new ModelNodeTransform
        {
            Position = node.LocalPosition,
            Rotation = node.LocalRotation,
            Scale = node.LocalScale,
            LocalMatrix = localMatrix,
            WorldMatrix = worldMatrix
        };

        // Recursively process children
        foreach (ModelNode child in node.Children)
        {
            BuildNodeTransformCache(child, worldMatrix);
        }
    }

    private void EvaluateAnimation(AnimationClip clip, float time)
    {
        // Reset all transforms to bind pose first
        BuildNodeTransformCache(Model.RootNode, Float4x4.Identity);

        // Apply animation to each bone
        foreach (AnimationClip.AnimBone bone in clip.Bones)
        {
            if (_nodeTransforms.TryGetValue(bone.BoneName, out ModelNodeTransform? nodeTransform))
            {
                // Evaluate animation curves at current time
                Float3 position = bone.EvaluatePositionAt(time);
                Quaternion rotation = bone.EvaluateRotationAt(time);
                Float3 scale = bone.EvaluateScaleAt(time);

                // Update the node transform
                nodeTransform.Position = position;
                nodeTransform.Rotation = rotation;
                nodeTransform.Scale = scale;
                nodeTransform.LocalMatrix = Float4x4.CreateTRS(position, rotation, scale);
            }
        }

        // Recalculate world matrices after animation update
        UpdateWorldMatrices(Model.RootNode, Float4x4.Identity);
    }

    private void UpdateWorldMatrices(ModelNode node, Float4x4 parentWorldMatrix)
    {
        if (_nodeTransforms.TryGetValue(node.Name, out ModelNodeTransform? nodeTransform))
        {
            nodeTransform.WorldMatrix = parentWorldMatrix * nodeTransform.LocalMatrix;

            // Recursively update children
            foreach (ModelNode child in node.Children)
            {
                UpdateWorldMatrices(child, nodeTransform.WorldMatrix);
            }
        }
    }

    private Float4x4[] CalculateBoneMatrices(ModelMesh modelMesh, Float4x4 meshWorldMatrix)
    {
        if (!modelMesh.HasBones || modelMesh.Mesh.bindPoses == null || modelMesh.Mesh.boneNames == null)
            return null;

        int boneCount = modelMesh.Mesh.bindPoses.Length;
        Float4x4[] boneMatrices = new Float4x4[boneCount];

        // Invert mesh world matrix to get mesh local space
        var meshLocalMatrix = (Float4x4)meshWorldMatrix.Invert();

        // Calculate bone transformation matrices
        for (int i = 0; i < boneCount; i++)
        {
            string boneName = modelMesh.Mesh.boneNames[i];
            Float4x4 bindPose = modelMesh.Mesh.bindPoses[i];

            // Try to find the bone transform from our cache
            if (_nodeTransforms.TryGetValue(boneName, out ModelNodeTransform? boneTransform))
            {
                // The final bone matrix formula for GPU skinning is:
                // boneMatrix = meshLocalMatrix * boneWorldMatrix * bindPose
                // This transforms from bind pose -> bone space -> world space -> mesh local space
                var boneWorldMatrix = (Float4x4)boneTransform.WorldMatrix;
                boneMatrices[i] = (meshLocalMatrix * boneWorldMatrix) * bindPose;
            }
            else
            {
                // If bone not found, use bind pose (no animation)
                boneMatrices[i] = bindPose;
            }
        }

        return boneMatrices;
    }

    private void RenderModelNode(ModelNode node, Float4x4 parentMatrix)
    {
        // Get the node's world matrix (from animation or bind pose)
        Float4x4 nodeWorldMatrix;
        if (_nodeTransforms.TryGetValue(node.Name, out ModelNodeTransform? nodeTransform))
        {
            // Use the animated/cached transform
            nodeWorldMatrix = parentMatrix * nodeTransform.LocalMatrix;
        }
        else
        {
            // Fallback to node's original transform
            var nodeLocalMatrix = Float4x4.CreateTRS(node.LocalPosition, node.LocalRotation, node.LocalScale);
            nodeWorldMatrix = parentMatrix * nodeLocalMatrix;
        }

        // Render all meshes on this node
        foreach (int meshIndex in node.MeshIndices)
        {
            ModelMesh modelMesh = Model.Meshes[meshIndex];

            if (modelMesh.Material.IsValid())
            {
                PropertyState properties = new();
                properties.SetInt("_ObjectID", InstanceID);
                properties.SetColor("_MainColor", MainColor);

                // Add bone matrices for skinned meshes
                if (modelMesh.HasBones)
                {
                    Float4x4[] boneMatrices = CalculateBoneMatrices(modelMesh, nodeWorldMatrix);
                    if (boneMatrices != null && boneMatrices.Length > 0)
                    {
                        // Convert to Float4x4 array for PropertyState
                        Float4x4[] boneMatricesDouble = [.. boneMatrices.Select(m => (Float4x4)m)];
                        properties.SetMatrices("boneTransforms", boneMatricesDouble);
                    }
                }

                GameObject.Scene.PushRenderable(new MeshRenderable(
                    modelMesh.Mesh,
                    modelMesh.Material,
                    nodeWorldMatrix,
                    GameObject.LayerIndex,
                    properties));
            }
        }

        // Render child nodes
        foreach (ModelNode child in node.Children)
        {
            RenderModelNode(child, nodeWorldMatrix);
        }
    }

    public bool Raycast(Ray ray, out float distance)
    {
        distance = float.MaxValue;

        if (Model.IsNotValid())
            return false;

        return RaycastModelNode(Model.RootNode, Transform.LocalToWorldMatrix, ray, ref distance);
    }

    private bool RaycastModelNode(ModelNode node, Float4x4 parentMatrix, Ray ray, ref float closestDistance)
    {
        bool hit = false;

        // Get the node's world matrix
        Float4x4 nodeWorldMatrix;
        if (_nodeTransforms.TryGetValue(node.Name, out ModelNodeTransform? nodeTransform))
        {
            nodeWorldMatrix = parentMatrix * nodeTransform.LocalMatrix;
        }
        else
        {
            var nodeLocalMatrix = Float4x4.CreateTRS(node.LocalPosition, node.LocalRotation, node.LocalScale);
            nodeWorldMatrix = parentMatrix * nodeLocalMatrix;
        }

        // Test all meshes on this node
        foreach (int meshIndex in node.MeshIndices)
        {
            ModelMesh modelMesh = Model.Meshes[meshIndex];

            if (modelMesh.Mesh.IsNotValid())
                continue;

            Mesh mesh = modelMesh.Mesh;

            // Transform ray to this mesh's local space
            Float4x4 worldToLocalMatrix = nodeWorldMatrix.Invert();

            Float3 localOrigin = Float4x4.TransformPoint(ray.Origin, worldToLocalMatrix);
            Float3 localDirection = Float4x4.TransformNormal(ray.Direction, worldToLocalMatrix);
            Ray localRay = new(localOrigin, localDirection);

            if (mesh.Raycast(localRay, out float localDistance))
            {
                // Calculate world space distance
                Float3 localHitPoint = localOrigin + localDirection * localDistance;
                Float3 worldHitPoint = Float4x4.TransformPoint(localHitPoint, nodeWorldMatrix);
                float worldDistance = Float3.Distance(ray.Origin, worldHitPoint);

                if (worldDistance < closestDistance)
                {
                    closestDistance = worldDistance;
                    hit = true;
                }
            }
        }

        // Test child nodes
        foreach (ModelNode child in node.Children)
        {
            if (RaycastModelNode(child, nodeWorldMatrix, ray, ref closestDistance))
                hit = true;
        }

        return hit;
    }
}
