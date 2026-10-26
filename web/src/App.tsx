import { useState } from "react";
import { ReplayViewer } from "./replay/ReplayViewer";
import { UnityMatch } from "./unity/UnityMatch";

type Tab = "unity" | "replay";

/**
 * React shell of the manager game. For now it hosts the Unity match (gameplay) and a dev replay viewer;
 * squad, tactics, transfers etc. will live here next to it.
 */
export function App() {
  const [tab, setTab] = useState<Tab>("unity");
  return (
    <div className="app">
      <header>
        <div className="brand">
          Soccer<span>Vision</span>
        </div>
        <nav>
          <button className={tab === "unity" ? "active" : ""} onClick={() => setTab("unity")}>
            Match (Unity)
          </button>
          <button className={tab === "replay" ? "active" : ""} onClick={() => setTab("replay")}>
            Engine replay (dev)
          </button>
        </nav>
        <div className="fixture">Red Rovers v Blue Falcons - 4-4-2 v 4-4-2</div>
      </header>
      <main>{tab === "unity" ? <UnityMatch /> : <ReplayViewer />}</main>
    </div>
  );
}
