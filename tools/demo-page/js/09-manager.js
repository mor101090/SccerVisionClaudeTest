  // ---- The manager: formation, the eleven, roles, individual instructions, set-piece takers, substitutions ----------
  // The plan goes to the engine as text (see TeamPlan.cs). Before kick-off it picks the eleven from the squad; during a
  // live match it reshapes the team at once and substitutions wait for the next stoppage (IFAB Law 3: five changes in
  // three opportunities, half-time free, no return).
  // Roles in the engine's order (PlayerRole): name and what the role does.
  var ROLES = [
    ["Auto", "Plays the position his own way - his attributes decide (a winger with his strong foot inside cuts in)."],
    ["Shot-stopper", "Lives on his line: sharp reflexes and positioning, stays home rather than rushing out."],
    ["Sweeper-keeper", "Comes far off his line to sweep up balls in behind and starts the play short from the back."],
    ["Dominant keeper", "Owns his six-yard box: comes for every cross and corner and claims it through the crowd."],
    ["Stopper", "Tough and aggressive: steps out of the line to win the ball early and attacks every header."],
    ["Ball-playing defender", "Breaks the first line: carries the ball out and plays long, accurate passes into the forwards."],
    ["Cover defender", "Reads the game and sweeps up a couple of metres behind his partner - never leaves the line."],
    ["Attacking full-back", "Lives on the touchline like an extra winger: endless runs up the flank to cross or join the box, relying on cover behind him."],
    ["Inverted full-back", "Comes inside into midfield when his team has the ball - numbers in the middle, the winger keeps the width."],
    ["Defensive full-back", "Defends first: rarely crosses halfway, wins his one-against-ones and can form a back three."],
    ["Destroyer", "Physical and aggressive: breaks up attacks with hard tackles and interceptions, little on the ball."],
    ["Deep-lying playmaker", "The brain from deep: sits in front of the defence and dictates with short and long passes."],
    ["Box-to-box", "An engine: wins the ball back deep, then drives forward to join the attack and press high."],
    ["Anchor", "Disciplined: stays in the middle, closes the lanes between the centre-backs and the midfield."],
    ["All-rounder", "Balanced: wins the ball, moves it on, gets forward and can shoot from distance."],
    ["Tempo setter", "Sets the rhythm: keeps the ball moving with simple, safe passes and organises the play."],
    ["Carrier", "Presses hard and drives forward with the ball at his feet to break the lines."],
    ["Classic playmaker", "The artist: drifts between the lines and plays the killer pass in behind for the forwards."],
    ["Shadow striker", "Wants goals, not the ball: bursts into the box from deep like a second striker."],
    ["Wide playmaker", "Starts wide or central and keeps drifting into the half-space to create an extra man."],
    ["Inverted winger", "Plays on his strong-foot side inside: cuts in to shoot or curl it at the far post."],
    ["Classic winger", "Quick and stays wide on the touchline: beats his man and crosses from the by-line."],
    ["Wide target man", "Physical and hard-working: presses the full-back, comes back to defend, the outlet for long balls."],
    ["Centre forward", "Lives closest to goal: his job is to turn chances into goals."],
    ["Target forward", "Tall and strong: always the highest man, the outlet for long balls, holds it up and lays it off."],
    ["Second striker", "Plays just under the striker: creates, plays through balls and arrives in the box."],
    ["False nine", "Drops from the front line into midfield, drags a centre-back with him and opens space for the runners."],
    ["Regista", "Drops in front of (or between) the centre-backs, sets the tempo, switches the play and clips it over the press; defends by reading the lanes."],
    ["Trequartista", "Free between the lines with hardly any defending: turns in tight spaces, plays the killer pass and shoots from the edge of the box."],
  ];
  var KIND_ROLES = [[0, 1, 2, 3], [0, 4, 5, 6], [0, 9, 8, 7], [0, 10, 11, 12, 13, 27], [0, 14, 15, 16], [0, 17, 18, 19, 28], [0, 20, 21, 22], [0, 23, 24, 25, 26]];