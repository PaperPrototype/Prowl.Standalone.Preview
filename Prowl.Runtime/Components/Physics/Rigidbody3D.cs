// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;

using Jitter2;
using Jitter2.Dynamics;
using Jitter2.LinearMath;

using Prowl.Echo;
using Prowl.Vector;

namespace Prowl.Runtime;

public sealed class Rigidbody3D : MonoBehaviour
{
    public class RigidBodyUserData
    {
        public Rigidbody3D Rigidbody { get; set; }
        public int Layer { get; set; }
        //public bool HasTransformConstraints { get; set; }
        //public JVector RotationConstraint { get; set; }
        //public JVector TranslationConstraint { get; set; }
    }

    [SerializeField] private bool isStatic;
    [SerializeField] private bool isSpeculative;
    [SerializeField] private bool useGravity = true;
    [SerializeField] private bool enableGyroscopicForces = false;
    [SerializeField] private double mass = 1;
    [SerializeField] private double linearDamping = 0.0;
    [SerializeField] private double angularDamping = 0.0;
    [SerializeField] private double friction = 0.2;
    [SerializeField] private double restitution = 0;
    [SerializeField] private double deactivationTime = 1.0;
    [SerializeField] private double linearSleepThreshold = 0.1;
    [SerializeField] private double angularSleepThreshold = 0.1;

    /// <summary>
    /// Gets or sets a value indicating whether this Rigidbody3D is static.
    /// </summary>
    public bool IsStatic
    {
        get => isStatic;
        set
        {
            isStatic = value;
            if (_body != null) _body.IsStatic = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether speculative contacts are enabled.
    /// </summary>
    public bool EnableSpeculativeContacts
    {
        get => isSpeculative;
        set
        {
            isSpeculative = value;
            if (_body != null) _body.EnableSpeculativeContacts = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this Rigidbody3D is affected by gravity.
    /// </summary>
    public bool AffectedByGravity
    {
        get => useGravity;
        set
        {
            useGravity = value;
            if (_body != null) _body.AffectedByGravity = value;
        }
    }

    /// <summary>
    /// Gets or sets the mass of this Rigidbody3D.
    /// </summary>
    public double Mass
    {
        get => mass;
        set
        {
            if (mass <= 0.0)
                throw new ArgumentException("Mass can not be zero or negative.", nameof(mass));

            mass = value;
            if (_body != null) _body.SetMassInertia(value);
        }
    }

    /// <summary>
    /// Gets or sets the friction of this Rigidbody3D.
    /// </summary>
    public double Friction
    {
        get => friction;
        set
        {
            if (value < 0.0 || value > 1.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Restitution must be between 0 and 1.");

            friction = value;
            if (_body != null) _body.Friction = value;
        }
    }

    /// <summary>
    /// Gets or sets the restitution of this Rigidbody3D.
    /// </summary>
    public double Restitution
    {
        get => restitution;
        set
        {
            if (value < 0.0 || value > 1.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Restitution must be between 0 and 1.");

            restitution = value;
            if (_body != null) _body.Restitution = value;
        }
    }

    /// <summary>
    /// Gets or sets the linear damping of this Rigidbody3D.
    /// Higher values slow down linear movement faster. Range: 0 to 1.
    /// </summary>
    public double LinearDamping
    {
        get => linearDamping;
        set
        {
            if (value < 0.0 || value > 1.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Linear damping must be between 0 and 1.");

            linearDamping = value;
            if (_body != null) _body.Damping = (linearDamping, _body.Damping.angular);
        }
    }

    /// <summary>
    /// Gets or sets the angular damping of this Rigidbody3D.
    /// Higher values slow down rotation faster. Range: 0 to 1.
    /// </summary>
    public double AngularDamping
    {
        get => angularDamping;
        set
        {
            if (value < 0.0 || value > 1.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Angular damping must be between 0 and 1.");

            angularDamping = value;
            if (_body != null) _body.Damping = (_body.Damping.linear, angularDamping);
        }
    }

    /// <summary>
    /// Gets or sets whether gyroscopic forces are enabled for this Rigidbody3D.
    /// Useful for spinning objects with high inertia anisotropy (like propellers).
    /// </summary>
    public bool EnableGyroscopicForces
    {
        get => enableGyroscopicForces;
        set
        {
            enableGyroscopicForces = value;
            if (_body != null) _body.EnableGyroscopicForces = value;
        }
    }

    /// <summary>
    /// Gets or sets the deactivation time in seconds.
    /// The body sleeps if velocity stays below threshold for this duration.
    /// </summary>
    public double DeactivationTime
    {
        get => deactivationTime;
        set
        {
            deactivationTime = value;
            if (_body != null) _body.DeactivationTime = System.TimeSpan.FromSeconds(value);
        }
    }

    /// <summary>
    /// Gets or sets the linear velocity threshold for sleeping.
    /// </summary>
    public double LinearSleepThreshold
    {
        get => linearSleepThreshold;
        set
        {
            linearSleepThreshold = value;
            if (_body != null) _body.DeactivationThreshold = (value, _body.DeactivationThreshold.angular);
        }
    }

    /// <summary>
    /// Gets or sets the angular velocity threshold for sleeping (radians/second).
    /// </summary>
    public double AngularSleepThreshold
    {
        get => angularSleepThreshold;
        set
        {
            angularSleepThreshold = value;
            if (_body != null) _body.DeactivationThreshold = (_body.DeactivationThreshold.linear, value);
        }
    }

    /// <summary>
    /// Gets whether this Rigidbody3D is currently active (not sleeping).
    /// </summary>
    public bool IsActive => _body?.IsActive ?? false;

    /// <summary>
    /// Gets or sets the Linear Velocity of this Rigidbody3D.
    /// </summary>
    public Double3 LinearVelocity
    {
        get => new(_body.Velocity.X, _body.Velocity.Y, _body.Velocity.Z);
        set => _body.Velocity = new(value.X, value.Y, value.Z);
    }

    /// <summary>
    /// Gets or sets the Angular Velocity of this Rigidbody3D.
    /// </summary>
    public Double3 AngularVelocity
    {
        get => new(_body.AngularVelocity.X, _body.AngularVelocity.Y, _body.AngularVelocity.Z);
        set => _body.AngularVelocity = new(value.X, value.Y, value.Z);
    }

    /// <summary>
    /// Gets or sets the Torque of this Rigidbody3D.
    /// </summary>
    public Double3 Torque
    {
        get => new(_body.Torque.X, _body.Torque.Y, _body.Torque.Z);
        set => _body.Torque = new JVector(value.X, value.Y, value.Z);
    }

    /// <summary>
    /// Event triggered when this rigidbody begins colliding with another rigidbody.
    /// </summary>
    public event Action<Rigidbody3D, ContactInfo> BeginCollide;

    /// <summary>
    /// Event triggered when this rigidbody stops colliding with another rigidbody.
    /// </summary>
    public event Action<Rigidbody3D> EndCollide;

    /// <summary>
    /// Information about a collision contact.
    /// </summary>
    public struct ContactInfo
    {
        public Double3 Point;
        public Double3 Normal;
        public double ImpulseMagnitude;
    }

    [SerializeIgnore]
    internal RigidBody _body;

    public RigidBody CreateBody(World world)
    {
        _body = world.CreateRigidBody();
        UpdateProperties(_body);
        UpdateShapes(_body);
        UpdateTransform(_body);
        _body.Tag = new RigidBodyUserData()
        {
            Rigidbody = this,
            Layer = GameObject.LayerIndex,
            //HasTransformConstraints = rotationConstraints != Vector3Int.one || translationConstraints != Vector3Int.one,
            //RotationConstraint = new JVector(rotationConstraints.x, rotationConstraints.y, rotationConstraints.z),
            //TranslationConstraint = new JVector(translationConstraints.x, translationConstraints.y, translationConstraints.z)
        };

        // Hook up collision events
        _body.BeginCollide += OnJitterBeginCollide;
        _body.EndCollide += OnJitterEndCollide;

        return _body;
    }

    private void OnJitterBeginCollide(Arbiter arbiter)
    {
        // Get the other body in the collision
        var otherBody = arbiter.Body1 == _body ? arbiter.Body2 : arbiter.Body1;

        if (otherBody.Tag is RigidBodyUserData userData && userData.Rigidbody != null)
        {
            // Get contact information from the first contact point if available
            var contact = arbiter.Handle.Data.Contact0;
            var normal = contact.Normal;
            var relPos = contact.RelativePosition2;

            // Calculate world position of contact
            var worldPos = otherBody.Position + relPos;

            var contactInfo = new ContactInfo
            {
                Point = new Double3(worldPos.X, worldPos.Y, worldPos.Z),
                Normal = new Double3(normal.X, normal.Y, normal.Z),
                ImpulseMagnitude = contact.Impulse
            };

            BeginCollide?.Invoke(userData.Rigidbody, contactInfo);
        }
    }

    private void OnJitterEndCollide(Arbiter arbiter)
    {
        // Get the other body in the collision
        var otherBody = arbiter.Body1 == _body ? arbiter.Body2 : arbiter.Body1;

        if (otherBody.Tag is RigidBodyUserData userData && userData.Rigidbody != null)
        {
            EndCollide?.Invoke(userData.Rigidbody);
        }
    }

    public override void OnValidate()
    {
        if (GameObject?.Scene?.IsNotValid() ?? true) return;

        if (_body == null || _body.Handle.IsZero)
            _body = GameObject.Scene.Physics.World.CreateRigidBody();

        UpdateProperties(_body);
        UpdateShapes(_body);
        UpdateTransform(_body);
    }

    public override void Update()
    {
        if (_body == null || _body.Handle.IsZero) return;

        Transform.Position = new Double3(_body.Position.X, _body.Position.Y, _body.Position.Z);
        Transform.Rotation = new Quaternion(_body.Orientation.X, _body.Orientation.Y, _body.Orientation.Z, _body.Orientation.W);
    }

    public override void DrawGizmos()
    {
        if (_body == null || _body.Handle.IsZero) return;

        _body.DebugDraw(JitterGizmosDrawer.Instance);
    }

    public override void OnEnable()
    {
        if (_body == null || _body.Handle.IsZero)
        {
            CreateBody(GameObject.Scene.Physics.World);
        }
    }

    public override void OnDisable()
    {
        if (_body != null && !_body.Handle.IsZero)
        {
            // Unhook collision events
            _body.BeginCollide -= OnJitterBeginCollide;
            _body.EndCollide -= OnJitterEndCollide;

            GameObject.Scene.Physics.World?.Remove(_body);
        }
    }

    internal void UpdateProperties(RigidBody rb)
    {
        rb.IsStatic = isStatic;
        rb.EnableSpeculativeContacts = isSpeculative;
        rb.Damping = (linearDamping, angularDamping);
        rb.Friction = friction;
        rb.AffectedByGravity = useGravity;
        rb.Restitution = restitution;
        rb.EnableGyroscopicForces = enableGyroscopicForces;
        rb.DeactivationTime = System.TimeSpan.FromSeconds(deactivationTime);
        rb.DeactivationThreshold = (linearSleepThreshold, angularSleepThreshold);
        rb.Tag = new RigidBodyUserData()
        {
            Rigidbody = this,
            Layer = GameObject.LayerIndex,
        };
        rb.SetMassInertia(mass);
    }

    internal void UpdateShapes(RigidBody rb)
    {
        rb.RemoveShape(rb.Shapes, false);
        foreach (Collider shape in GetComponents<Collider>())
        {
            Jitter2.Collision.Shapes.RigidBodyShape[] result = shape.CreateTransformedShapes();
            if (result == null) continue;
            foreach (Jitter2.Collision.Shapes.RigidBodyShape s in result)
                rb.AddShape(s, false);
        }
    }

    internal void UpdateTransform(RigidBody rb)
    {
        if (GameObject?.Scene?.Physics.AutoSyncTransforms ?? true)
        {
            rb.Position = new JVector(Transform.Position.X, Transform.Position.Y, Transform.Position.Z);
            rb.Orientation = new JQuaternion(Transform.Rotation.X, Transform.Rotation.Y, Transform.Rotation.Z, Transform.Rotation.W);
        }
    }

    public void AddForce(Double3 velocity)
    {
        _body.AddForce(new JVector(velocity.X, velocity.Y, velocity.Z));
    }

    public void AddForceAtPosition(Double3 velocity, Double3 worldPosition)
    {
        _body.AddForce(new JVector(velocity.X, velocity.Y, velocity.Z), new JVector(worldPosition.X, worldPosition.Y, worldPosition.Z));
    }

    public void AddTorque(Double3 torque)
    {
        Torque += torque;
    }

    /// <summary>
    /// Sets the activation state of this rigidbody (awake or sleeping).
    /// </summary>
    public void SetActive(bool active)
    {
        if (_body != null)
            _body.SetActivationState(active);
    }

    /// <summary>
    /// Gets the velocity at a world space point on this rigidbody.
    /// </summary>
    public Double3 GetPointVelocity(Double3 worldPoint)
    {
        if (_body == null) return Double3.Zero;

        var point = new JVector(worldPoint.X, worldPoint.Y, worldPoint.Z);
        JVector r = point - _body.Position;
        JVector velocity = _body.Velocity + JVector.Cross(_body.AngularVelocity, r);

        return new Double3(velocity.X, velocity.Y, velocity.Z);
    }

    /// <summary>
    /// Gets the center of mass in world space.
    /// </summary>
    public Double3 CenterOfMass
    {
        get
        {
            if (_body == null) return Transform.Position;
            JVector pos = _body.Position;
            return new Double3(pos.X, pos.Y, pos.Z);
        }
    }

    /// <summary>
    /// Gets the inertia tensor (inverse) of this rigidbody.
    /// </summary>
    public Double3 InertiaTensor
    {
        get
        {
            if (_body == null) return Double3.One;
            JMatrix inertia = _body.InverseInertia;
            return new Double3(
                inertia.M11 != 0 ? 1.0 / inertia.M11 : 0,
                inertia.M22 != 0 ? 1.0 / inertia.M22 : 0,
                inertia.M33 != 0 ? 1.0 / inertia.M33 : 0
            );
        }
    }

    /// <summary>
    /// Applies an impulse at a position, immediately affecting velocity.
    /// </summary>
    public void ApplyImpulse(Double3 impulse, Double3 worldPosition)
    {
        if (_body == null) return;

        var jImpulse = new JVector(impulse.X, impulse.Y, impulse.Z);
        var jPosition = new JVector(worldPosition.X, worldPosition.Y, worldPosition.Z);

        JVector r = jPosition - _body.Position;
        _body.Velocity += jImpulse * _body.Data.InverseMass;
        _body.AngularVelocity += JVector.Transform(JVector.Cross(r, jImpulse), _body.Data.InverseInertiaWorld);

        SetActive(true);
    }

    /// <summary>
    /// Applies an impulse to the rigidbody, immediately affecting velocity.
    /// </summary>
    public void ApplyImpulse(Double3 impulse)
    {
        if (_body == null) return;

        var jImpulse = new JVector(impulse.X, impulse.Y, impulse.Z);
        _body.Velocity += jImpulse * _body.Data.InverseMass;

        SetActive(true);
    }

    /// <summary>
    /// Applies an angular impulse to the rigidbody, immediately affecting angular velocity.
    /// </summary>
    public void ApplyAngularImpulse(Double3 angularImpulse)
    {
        if (_body == null) return;

        var jImpulse = new JVector(angularImpulse.X, angularImpulse.Y, angularImpulse.Z);
        _body.AngularVelocity += JVector.Transform(jImpulse, _body.Data.InverseInertiaWorld);

        SetActive(true);
    }

    /// <summary>
    /// Moves the rigidbody to a new position (teleport).
    /// </summary>
    public void MovePosition(Double3 position)
    {
        if (_body != null)
        {
            _body.Position = new JVector(position.X, position.Y, position.Z);
        }
    }

    /// <summary>
    /// Rotates the rigidbody to a new rotation (teleport).
    /// </summary>
    public void MoveRotation(Quaternion rotation)
    {
        if (_body != null)
        {
            _body.Orientation = new JQuaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
        }
    }
}
