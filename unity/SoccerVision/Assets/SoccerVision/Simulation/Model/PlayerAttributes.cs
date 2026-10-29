using System;

namespace SoccerVision.Simulation
{
    public enum FitnessLevel { Low, Normal, High }

    public enum Foot { Left, Right }

    public enum PositionGroup { Goalkeeper, Defender, Midfielder, Forward }

    /// <summary>
    /// Player skills as defined in the design document. Every sub-skill is 0..100, feet are rated 1..3.
    /// Category values (MENTAL, DEFENCE, PLAYMAKING, ATTACKING, ATHLETIC) are the average of their sub-skills.
    /// </summary>
    [Serializable]
    public sealed class PlayerAttributes
    {
        // MENTAL
        public int Aggression = 50;
        public int Teamwork = 50;
        public int Composure = 50;
        public int Decisions = 50;

        // DEFENCE
        public int Tackling = 50;
        public int Marking = 50;
        public int Positioning = 50;

        // PLAYMAKING
        public int Creativity = 50;
        public int Passing = 50;
        public int Crossing = 50;

        // ATTACKING
        public int Finishing = 50;
        public int LongShots = 50;
        public int OffTheBall = 50;

        // ATHLETIC
        public int PhysicalPower = 50;
        public int BallControl = 50;
        public int Dribbling = 50;
        public int Acceleration = 50;
        public int Speed = 50;
        public int Jumping = 50;
        public int Heading = 50;

        // FORM
        public int Stamina = 50;
        public FitnessLevel Fitness = FitnessLevel.Normal;

        // SET PIECES
        public int FreeKicks = 50;
        public int Penalty = 50;

        // FOOT PREFERENCE (1 = weak, 3 = excellent)
        public int LeftFoot = 1;
        public int RightFoot = 3;

        // GOALKEEPING
        public int AerialAbility = 20;
        public int Handling = 20;
        public int Kicking = 20;
        public int OneOnOne = 20;
        public int Reflexes = 20;

        public float Mental => (Aggression + Teamwork + Composure + Decisions) / 4f;
        public float Defence => (Tackling + Marking + Positioning) / 3f;
        public float Playmaking => (Creativity + Passing + Crossing) / 3f;
        public float Attacking => (Finishing + LongShots + OffTheBall) / 3f;
        public float Athletic => (PhysicalPower + BallControl + Dribbling + Acceleration + Speed + Jumping + Heading) / 7f;
        public float Goalkeeping => (AerialAbility + Handling + Kicking + OneOnOne + Reflexes) / 5f;

        public Foot StrongFoot => RightFoot >= LeftFoot ? Foot.Right : Foot.Left;
        public int StrongFootRating => Math.Max(LeftFoot, RightFoot);
        public int WeakFootRating => Math.Min(LeftFoot, RightFoot);

        public PlayerAttributes Clone() => (PlayerAttributes)MemberwiseClone();
    }

    [Serializable]
    public sealed class PlayerProfile
    {
        public int Id;
        public string FirstName = "";
        public string LastName = "";
        public int ShirtNumber;
        public PositionGroup NaturalPosition;
        /// <summary>The position as a team sheet shows it (GK, CB, RB, LB, DM, CM, AM, RW, LW, ST); empty: from the group.</summary>
        public string Position = "";
        public string PositionLabel => !string.IsNullOrEmpty(Position) ? Position : NaturalPosition == PositionGroup.Goalkeeper ? "GK" : NaturalPosition == PositionGroup.Defender ? "CB" : NaturalPosition == PositionGroup.Midfielder ? "CM" : "ST";
        public int HeightCm = 180;
        /// <summary>Age and nationality (from the team generator). Age shapes development between matches (16-21 improve
        /// most, 22-29 a little, 30+ decline); it does not change a single match.</summary>
        public int Age = 25;
        public string Nationality = "";
        public PlayerAttributes Attributes = new PlayerAttributes();

        public string ShortName => string.IsNullOrEmpty(LastName) ? FirstName : LastName;
        public string FullName => string.IsNullOrEmpty(FirstName) ? LastName : FirstName + " " + LastName;
        public string Label => ShirtNumber + " " + ShortName;
    }
}
