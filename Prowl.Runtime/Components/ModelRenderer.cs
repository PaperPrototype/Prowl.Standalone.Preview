// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Linq;

using Prowl.Runtime.Audio.Native;
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
    private Pose _currentPose;

    public override void OnEnable()
    {
        if (Model.IsValid())
        {
            // Initialize bind pose if skeleton exists
            if (Model.Skeleton.IsValid())
            {
                _currentPose = Pose.CreateBindPose(Model.Skeleton);
            }

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

            // Get the current pose from the animation
            _currentPose = CurrentAnimation.GetPose(_animationTime);
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

    private Float4x4[] CalculateBoneMatrices(Float4x4 meshWorldMatrix)
    {
        if (_currentPose == null || Model.Skeleton.IsNotValid())
            return null;

        // Get bone matrices from the pose using the skeleton
        // These are in skeleton-local space and already include the bind pose offset
        Float4x4[] boneMatrices = _currentPose.GetBoneMatrices(Model.Skeleton);

        return boneMatrices;
    }

    private void RenderModelNode(ModelNode node, Float4x4 parentMatrix)
    {
        // Calculate this node's world matrix
        var nodeLocalMatrix = Float4x4.CreateTRS(node.LocalPosition, node.LocalRotation, node.LocalScale);
        Float4x4 nodeWorldMatrix = parentMatrix * nodeLocalMatrix;

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
                    Float4x4[] boneMatrices = CalculateBoneMatrices(nodeWorldMatrix);
                    if (boneMatrices != null && boneMatrices.Length > 0)
                    {
                        properties.SetMatrices("boneTransforms", boneMatrices);
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

        // Calculate this node's world matrix
        var nodeLocalMatrix = Float4x4.CreateTRS(node.LocalPosition, node.LocalRotation, node.LocalScale);
        Float4x4 nodeWorldMatrix = parentMatrix * nodeLocalMatrix;

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



    #region Debug Drawing

    public override void DrawGizmos()
    {
        if (Model.IsNotValid() || Model.Skeleton.IsNotValid() || _currentPose == null)
            return;

        DrawSkeleton();
    }

    private void DrawSkeleton()
    {
        Float4x4 worldTransform = Transform.LocalToWorldMatrix;
        Skeleton skeleton = Model.Skeleton;

        // Calculate world transforms for all bones from the current pose
        skeleton.CalculateWorldTransforms(
            _currentPose.LocalPositions,
            _currentPose.LocalRotations,
            _currentPose.LocalScales,
            out Float4x4[] boneWorldTransforms);

        // Draw each bone
        for (int i = 0; i < skeleton.Bones.Count; i++)
        {
            Skeleton.Bone bone = skeleton.Bones[i];

            // Transform bone to world space
            Float4x4 boneWorldMatrix = worldTransform * boneWorldTransforms[i];
            Float4 bonePosF4 = boneWorldMatrix.Translation;
            Float3 bonePos = new Float3(bonePosF4.X, bonePosF4.Y, bonePosF4.Z);

            if (bone.ParentID >= 0)
            {
                // Get parent transform in world space
                Float4x4 parentWorldMatrix = worldTransform * boneWorldTransforms[bone.ParentID];
                Float4 parentPosF4 = parentWorldMatrix.Translation;
                Float3 parentPos = new Float3(parentPosF4.X, parentPosF4.Y, parentPosF4.Z);

                // Draw line from parent to this bone
                Debug.DrawLine(parentPos, bonePos, Color.Cyan);

                // Draw bone as a pyramid from parent to this position
                DrawBone(parentPos, bonePos, Color.Yellow);
            }
            else
            {
                // Root bone - just draw a sphere
                Debug.DrawWireSphere(bonePos, 0.03f, Color.Red);
            }

            // Draw a small sphere at each bone position
            Debug.DrawWireSphere(bonePos, 0.02f, Color.Yellow);
        }
    }

    private void DrawBone(Float3 parentPos, Float3 childPos, Color color)
    {
        // Calculate bone direction and length
        Float3 direction = childPos - parentPos;
        float length = Float3.Length(direction);

        if (length < 0.001f)
            return; // Skip zero-length bones

        Float3 normalizedDir = direction / length;

        // Draw a pyramid from parent to child
        // Base at parent, tip at child
        float baseRadius = length * 0.1f; // 10% of bone length

        // Create a perpendicular vector for the base
        Float3 perpendicular = Maths.Abs(normalizedDir.Y) < 0.9f
            ? Float3.Cross(normalizedDir, Float3.UnitY)
            : Float3.Cross(normalizedDir, Float3.UnitX);
        perpendicular = Float3.Normalize(perpendicular);

        Float3 perpendicular2 = Float3.Cross(normalizedDir, perpendicular);

        // Create 4 points around the base
        Float3[] basePoints = new Float3[4];
        for (int i = 0; i < 4; i++)
        {
            float angle = (i * 90f) * Maths.PI / 180f;
            Float3 offset = (perpendicular * Maths.Cos(angle) + perpendicular2 * Maths.Sin(angle)) * baseRadius;
            basePoints[i] = parentPos + offset;
        }

        // Draw pyramid edges from base to tip
        for (int i = 0; i < 4; i++)
        {
            Debug.DrawLine(basePoints[i], childPos, color);
        }

        // Draw base square
        for (int i = 0; i < 4; i++)
        {
            Debug.DrawLine(basePoints[i], basePoints[(i + 1) % 4], color);
        }
    }

    #endregion
}
