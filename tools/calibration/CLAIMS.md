# What we believe about real football, and how we know

The engine is tuned against real football, so a wrong number here makes the game wrong in a way nobody notices. Every figure used to tune or to describe the engine is listed with where it
came from and how sure we are. Add a line when a number is used; change its status when it is shown wrong. **Verified** means counted from raw data by a script in this repository that
anyone can run; **Secondary** means read from an article or a search result (not checked against the original data); **Measured here** means a measurement of the engine, with its sample size.

## Rules for a number that goes into a report or a constant
1. **Say what kind of number it is**: counted (script and data named), read from a source (link; secondary unless the original data was checked), measured on the engine (matches, seed), or a guess (said so).
2. **Compare like with like**: the same population on both sides (a table tier is not an odds bucket; a seat-pooled result is not one seat), the same definition, the same unit.
3. **Rule out the obvious confound before naming a cause**: swap the seats, swap the squads, run the default and the changed version on the same seeds.
4. **Give the sample size and the noise**: 100 matches move a win rate by about 5 points on their own; no constant is changed on one run of fewer than 150 matches a cell, and not unless the
   direction repeats on a second seed.
5. **A change that should not move the ordinary match must leave it identical to the digit** (the same matches before and after); if it does not, find out why before going on.
6. **Wrong statements are corrected where they were written** (the CHANGELOG entry), not only in the next one.
7. **The results of the tests are quoted from the run's last line** ("Passed! - Failed: 0 ..."), never from memory.

## Claims
| Claim | Source / method | Status |
|---|---|---|
| Real home advantage: home wins 43-45%, draws 23-26%, away wins 28-33%, goals home - away about 1.5 - 1.2, in eight leagues, 2015/16-2024/25; gone in 2020/21 (empty stadiums, 38% v 40%) | `tools/calibration/real_leagues.py`, data from football-data.co.uk | Verified |
| Tier results (top two / 7th-14th / bottom three), pooled seven leagues: strong v weak 85 / 11 / 4, strong v medium 73 / 17 / 10, medium v weak 54 / 27 / 19 | the same script | Verified (an earlier table of the same had a different medium for 18-team leagues and a hand-averaged last row: corrected) |
| The engine's home advantage is about the real size (+12 points of win share, +0.2-0.36 goals, seats swapped, 600 matches a tactic) | `SimRunner --swap` | Measured here |
| "The engine's home advantage is twice the real one (58% v 44%)" | read straight off one pairing | **Refuted**: that was Team A being the better side (zonal v man-to-man marking); seats swapped, the edge is the real size |
| "Possession near 50% in every pairing, as in real football" | said without a source | **Refuted**: possession follows style and quality in real football; fixed in the possession round |
| Possession by table tier, 2015/16 (top two v bottom three / v 7th-14th / 7th-14th v bottom three): PL 53.8 / 49.9 / 52.9%, La Liga 60.4 / 62.1 / 50.4%; equal mid-table sides 36-64% match to match (10%-90%) | `tools/calibration/possession_tiers.py`, StatsBomb open data (full seasons, 380 matches each) | Verified (one season a league; tier cells of 12-48 matches) |
| Possession by the score: a goal down 55.7% (PL) / 56.7% (La Liga), a goal up 44.3 / 43.3%, two or more apart 52-53 / 47-48% | the same script | Verified (2015/16 only) |
| Pass share and share of time on the ball agree within about a point in real matches | the same script (both measures) | Verified |
| The engine after the score round: a goal down 57.9% (default tactics) / 53.7% (random match); random match strong v weak 55.0%, medium v weak 51.3% | `SimRunner --poss [--random]`, 150 matches, seed 2000 | Measured here |
| A season's most possession-heavy side has about 64-70% of the ball; the record is Liverpool 83.1% v Sheffield United (2024) | StatMuse / Khel Now, via a search | Secondary: not checked against Opta |
| At the 2022 men's World Cup the sides with less of the ball won 26 matches to 23 and took 1.45 points a game to 1.31 | FIFA research page, read through a summarising tool | Secondary: the page's figures were summarised, not read in full |
| Weak / strong squads "moved 60% / 80% of the way" because strong v medium should be 57% | compared a tier result with an odds bucket (a clear favourite) | **Wrong benchmark**: re-fitted to the table tiers (strong 120%, weak 50%) |
| Shot mechanics (where goals come from, conversion) | StatsBomb open data, five leagues 2015/16, about 40 matches each (`tools/calibration/statsbomb_targets.py`) | Verified for that sample; says nothing about results or home advantage |
| Per match, Premier League 2015/16-2024/25 (both teams): goals 2.83, shots 25.4, on target 8.8, fouls 21.5, corners 10.4, yellow cards 3.44, red cards 0.12 (other leagues in the script's output) | `tools/calibration/real_leagues.py` | Verified |
| The "real-world reference" column of `SimRunner --diag` for passes (850-1000), tackles (30-40, 60-65% won), distance (105-115 km a team), throw-ins (39-43), goal kicks (14-18), woodwork, punches | written in the code without a source | **Unverified**: to be sourced or marked as estimates before anything is tuned to them |
