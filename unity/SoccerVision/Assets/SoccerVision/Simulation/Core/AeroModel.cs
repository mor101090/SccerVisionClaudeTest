using System;

namespace SoccerVision.Simulation
{
    /// <summary>
    /// The aerodynamic coefficients of a size 5 football as a function of the Reynolds number and the spin parameter: a coefficient
    /// model (tables read from measurements), not CFD. Every table says where it comes from; see docs/BALL_PHYSICS.md for the
    /// classes (A standardised, B measured, C derived, D approximation, E unknown).
    ///
    /// Re = rho * v * D / mu (v: the ball's speed through the air, D: its diameter, mu: the air's dynamic viscosity).
    /// Sp = R * w_perp / v (w_perp: the part of the angular velocity perpendicular to the velocity; spin about the line of flight
    /// makes no sideways force).
    /// </summary>
    public sealed class AeroModel
    {
        // ---- Drag crisis: Cd0(Re), no spin ----------------------------------------------------------------------------------
        // Source: the two wind-tunnel series plotted in Goff and Carre, Am. J. Phys. 77 (2009) 1020, Fig. 6 (read from the figure
        // by eye, +-0.03): Asai et al. 2007 (a 32-panel ball: Cd 0.46 up to Re 1.5e5, down to 0.14-0.15 at Re 2.3-3e5, back up to
        // 0.19 at 4.5e5) and Carre et al. 2005 (a scaled ball: the crisis at Re 0.9-1.3e5, then 0.20-0.25 up to 4.7e5). The paper
        // itself measured Cd = 0.2 (v = 18 m/s, Re 2.6e5) and Cd = 0.17 (Re 2.23-2.76e5) from trajectories. The points below are a
        // reference profile chosen BETWEEN the two series (class D): the crisis between Re 1.2e5 and 1.8e5, 0.19-0.20 after it.
        // Different balls differ by +-0.05 in the crisis and after it; that spread is the uncertainty of this table.
        private static readonly float[] CdRe = { 0.8e5f, 1.2e5f, 1.5e5f, 1.8e5f, 2.2e5f, 3.0e5f, 4.0e5f, 5.0e5f };
        private static readonly float[] CdVal = { 0.46f, 0.44f, 0.34f, 0.22f, 0.19f, 0.18f, 0.19f, 0.20f };

        // ---- Spin dependence of drag ---------------------------------------------------------------------------------------
        // Goff and Carre Fig. 7 (wind tunnel, Re 3.3-4.5e5, after the crisis): Cd rises from 0.16-0.18 at Sp 0.05 to 0.29 at
        // Sp 0.31, i.e. about +0.45 per unit of Sp on top of the no-spin value (a straight-line fit to the figure, class D). The figure
        // stops at Sp 0.31; beyond it the same paper's trajectory fits use Cd 0.27-0.30 up to Sp 0.71 (taken from that same figure, so no
        // independent evidence). A straight line would give 0.4 and more there, so the added drag is capped at 0.13 (total about 0.31).
        public float DragPerSpinParameter = 0.45f;
        public float MaxSpinDrag = 0.13f;

        // ---- Magnus (lift / side force): CL(Sp) ---------------------------------------------------------------------------
        // Goff and Carre Fig. 8: the wind-tunnel |CL| (Asai et al., five Re from 3.3e5 to 4.6e5) rises from about 0.05-0.15 at
        // Sp 0.05 to 0.25-0.33 at Sp 0.3, and their own trajectory estimates are 0.27-0.30 at Sp 0.24-0.25, 0.29 at 0.49 and
        // 0.31 at 0.71 (+-30%, Re 2.6-3.1e5): the coefficient flattens near 0.3. Mean of the points, read by eye (class B,
        // +-0.04). The same coefficient is used for the side force (spin about a vertical axis): the paper argues both come from
        // the same physics (CL and CS are the same function of Sp; only the direction differs).
        private static readonly float[] ClSp = { 0f, 0.05f, 0.10f, 0.15f, 0.20f, 0.30f, 0.50f, 0.71f, 1.0f };
        private static readonly float[] ClVal = { 0f, 0.09f, 0.17f, 0.23f, 0.26f, 0.29f, 0.30f, 0.31f, 0.31f };

        /// <summary>
        /// Spin decay in flight, 1/s (exponential). Goff and Carre estimate the spin rate falls by 4-8% by the apex and by about
        /// 10% by landing (a flight of about 1.5 s), citing James and Haake 2008 for "roughly exponential": 0.07-0.1 1/s
        /// (class B, secondary). The engine had 0.25.
        /// </summary>
        public float SpinDecayPerSecond = 0.08f;

        /// <summary>Multiplies every drag coefficient (1 = the table). For calibration against a different ball.</summary>
        public float DragScale = 1f;
        /// <summary>Multiplies every Magnus coefficient (1 = the table).</summary>
        public float LiftScale = 1f;

        /// <summary>The reference profile (see the tables above).</summary>
        public static readonly AeroModel Reference = new AeroModel();

        /// <summary>Drag coefficient of a non-spinning ball at this Reynolds number (linear between the table's points, constant beyond them).</summary>
        public float Cd0(float reynolds) => DragScale * Interpolate(CdRe, CdVal, reynolds);

        /// <summary>Drag coefficient at this Reynolds number and spin parameter.</summary>
        public float Cd(float reynolds, float spinParameter) => Cd0(reynolds) + DragScale * MathF.Min(MaxSpinDrag, DragPerSpinParameter * spinParameter);

        /// <summary>Magnus coefficient (lift for backspin / topspin, side force for sidespin) at this spin parameter.</summary>
        public float Cl(float spinParameter) => LiftScale * Interpolate(ClSp, ClVal, spinParameter);

        /// <summary>Reynolds number of a ball of this diameter moving at this speed through air of this density and dynamic viscosity.</summary>
        public static float ReynoldsNumber(float airDensity, float speed, float diameter, float dynamicViscosity)
            => airDensity * speed * diameter / dynamicViscosity;

        private static float Interpolate(float[] xs, float[] ys, float x)
        {
            if (x <= xs[0]) return ys[0];
            int last = xs.Length - 1;
            if (x >= xs[last]) return ys[last];
            int i = 1;
            while (xs[i] < x) i++;
            float f = (x - xs[i - 1]) / (xs[i] - xs[i - 1]);
            return ys[i - 1] + (ys[i] - ys[i - 1]) * f;
        }
    }
}
