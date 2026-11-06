/** Skill groups and labels shown in the player drawer and the player profile (generator stat keys). Server-safe. */
export const DETAIL_GROUPS: { title: string; group: string; keys: [string, string][] }[] = [
  {
    title: "GOALKEEPING",
    group: "goalkeeping",
    keys: [
      ["aerial_ability", "Aerial ability"],
      ["handling", "Handling"],
      ["kicking", "Kicking"],
      ["one_on_one", "One on one"],
      ["reflexes", "Reflexes"],
    ],
  },
  {
    title: "DEFENSE",
    group: "defense",
    keys: [
      ["tackling", "Tackling"],
      ["marking", "Marking"],
      ["positioning", "Positioning"],
    ],
  },
  {
    title: "ATHLETIC",
    group: "athletic",
    keys: [
      ["physical_power", "Physical power"],
      ["ball_control", "Ball control"],
      ["dribbling", "Dribbling"],
      ["jumping", "Jumping"],
      ["acceleration", "Acceleration"],
      ["speed", "Speed"],
      ["heading", "Heading"],
    ],
  },
  {
    title: "PLAYMAKING",
    group: "playmaking",
    keys: [
      ["creative", "Creativity"],
      ["passing", "Passing"],
      ["crossing", "Crossing"],
    ],
  },
  {
    title: "SET PIECES",
    group: "set_pieces",
    keys: [
      ["free_kicks", "Free kicks"],
      ["penalty", "Penalty"],
    ],
  },
  {
    title: "MENTAL",
    group: "mental",
    keys: [
      ["aggression", "Aggression"],
      ["teamwork", "Teamwork"],
      ["decisions", "Decisions"],
      ["composure", "Composure"],
    ],
  },
  {
    title: "ATTACK",
    group: "attack",
    keys: [
      ["finishing", "Finishing"],
      ["long_shots", "Long shots"],
      ["off_the_ball", "Off the ball"],
    ],
  },
];
