using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// What the ball does when it meets the goal frame or a player (ball physics stage 8). See docs/BALL_PHYSICS.md for the classes
    /// (A standardised, B measured, C derived, D approximation, E unknown).
    ///
    /// Every contact is the same impulse as the bounce of stage 4, about the contact normal: the normal speed is turned back times the
    /// restitution e, and friction at the contact point (up to the Coulomb limit mu x the normal impulse) works on the slip, changing both
    /// the speed and the spin (L = I w). The ball is found against the frame by a swept test over the whole step, so a fast shot cannot
    /// pass through a 12 cm post between two steps.
    /// </summary>
    public sealed class CollisionModel
    {
        /// <summary>
        /// The ball's restitution against a rigid surface at the FIFA reference impact (6.26 m/s): FIFA sets the ball's pressure so that it
        /// rebounds 1.45 m on concrete from 2.0 m, e = sqrt(1.45 / 2.0) = 0.85 (class C from A). A steel or aluminium post is as rigid as
        /// concrete for the ball: all the loss is in the ball.
        /// </summary>
        public float FrameRestitution = 0.85f;
        /// <summary>The FIFA drop test's impact speed, sqrt(2 g 2.00 m) (class C).</summary>
        public float ReferenceImpactSpeed = 6.26f;
        /// <summary>
        /// How much the restitution falls per m/s of normal impact speed above the reference: the ground model's 0.01 (class E, by analogy with
        /// pressurised tennis balls on a rigid plate; no football measurement could be read). A shot hitting the post square at 25 m/s keeps
        /// e = 0.66 (the old model used 0.7 for every impact).
        /// </summary>
        public float RestitutionPerMetrePerSecond = 0.01f;
        /// <summary>Friction between the ball's cover and a painted post (class E: no measurement found; a value that lets a glancing hit grip a little).</summary>
        public float FrameFriction = 0.3f;

        /// <summary>
        /// The radius of the body a ball strikes when it hits a player unintentionally (a block, a deflection): the trunk and the legs together,
        /// as one vertical cylinder (class D). Only the direction of the contact normal depends on it.
        /// </summary>
        public float BodyRadius = 0.2f;
        /// <summary>Friction between the ball and a player's kit or skin (class E).</summary>
        public float BodyFriction = 0.4f;

        /// <summary>The ball's restitution against the frame at this normal impact speed.</summary>
        public float FrameRestitutionAt(float normalSpeed)
            => MathUtil.Clamp(FrameRestitution - RestitutionPerMetrePerSecond * (normalSpeed - ReferenceImpactSpeed), 0.3f, 0.95f);

        /// <summary>The reference collisions (the values above).</summary>
        public static CollisionModel Reference => new CollisionModel();
    }
}
