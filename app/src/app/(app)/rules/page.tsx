import type { Metadata } from "next";

import { Card, PageHeader } from "@/components/app-shell/ui";

export const metadata: Metadata = { title: "Game rules" };

const SECTIONS: [string, string, [string, string][]][] = [
  [
    "matches",
    "Matches",
    [
      [
        "Length",
        "Two halves of 10 real minutes plus added time, with a 2-minute break. The match engine is deterministic: the same seed and the same teams play the same match.",
      ],
      [
        "Friendlies",
        "Play the computer from My Games, or a friend online with a six-digit game code. Friendlies count for your form, stats and fan mood.",
      ],
      ["Replays", "Every match you play with your club is saved with its replay under My Games."],
    ],
  ],
  [
    "lineups",
    "Lineups & substitutions",
    [
      [
        "Starting XI",
        "Eleven starters and up to nine substitutes. Your saved lineup is used when you kick off.",
      ],
      ["Substitutions", "A player who has come off cannot come back on."],
      ["Captain", "Pick a captain in your squad; he leads the team on the pitch."],
    ],
  ],
  [
    "tactics",
    "Tactics",
    [
      [
        "Formation",
        "Twelve shapes. Before kick-off every starter takes the nearest position of the new shape.",
      ],
      [
        "Team instructions",
        "Defensive line, width, passing, tempo, aggression, defence, attack and style. Some choices don't go together (for example parking the bus with a fast tempo) and are greyed out.",
      ],
      [
        "Player instructions",
        "A role per position plus long shots, dribbling and aggression (less, mixed, more).",
      ],
      [
        "Set pieces",
        "First and second takers for corners and free kicks on each side, and for penalties.",
      ],
    ],
  ],
  [
    "account",
    "Account & fair play",
    [
      ["One club per manager", "Each account manages one club."],
      [
        "Fair play",
        "Matches are verified by re-simulating them from their seed. Tampered results are not saved.",
      ],
    ],
  ],
];

export default function RulesPage() {
  return (
    <div className="flex flex-col gap-5">
      <PageHeader crumb="Rules" title="Game rules" />
      <div className="flex flex-col gap-5 lg:flex-row lg:items-start">
        <Card title="Contents" className="lg:sticky lg:top-24 lg:w-[260px]">
          <nav aria-label="Rules contents" className="flex flex-col gap-1">
            {SECTIONS.map(([id, title], i) => (
              <a
                key={id}
                href={`#${id}`}
                className="rounded-lg px-2.5 py-2 text-[13px] font-medium text-muted hover:bg-panel hover:text-text"
              >
                {i + 1}. {title}
              </a>
            ))}
          </nav>
        </Card>
        <div className="flex flex-1 flex-col gap-5">
          {SECTIONS.map(([id, title, items], i) => (
            <Card key={id} id={id} delay={i} className="px-8! py-7!">
              <h2 className="text-[22px] font-extrabold text-text">
                {i + 1}. {title}
              </h2>
              {items.map(([h, b]) => (
                <div key={h} className="flex flex-col gap-1">
                  <h3 className="text-[15px] font-bold text-text">{h}</h3>
                  <p className="text-sm leading-[22px] text-muted">{b}</p>
                </div>
              ))}
            </Card>
          ))}
        </div>
      </div>
    </div>
  );
}
