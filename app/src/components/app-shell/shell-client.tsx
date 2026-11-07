"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useRef, useState, useSyncExternalStore, useTransition } from "react";

import { signOutAction } from "@/features/auth/actions";
import { markNotificationsReadAction } from "@/features/home/actions";
import { badge, initials } from "@/features/home/rules";
import { Crest } from "@/features/team/ui/pieces";
import { relativeTime } from "@/lib/relative-time";
import type { ShellData, ShellNotification } from "@/server/shell";

import { AppIcon, type IconName } from "./app-icon";
import logo from "../../../public/brand/logo.png";

type NavItem = { label: string; href: string; icon: IconName; match?: string[] };

/** Sidebar routes (Figma "Sidebar"). `match`: other paths that light up the same item. */
export const NAV: NavItem[] = [
  {
    label: "Home",
    href: "/home",
    icon: "home",
    match: ["/league", "/rankings", "/finances", "/stadium"],
  },
  { label: "Squad", href: "/squad", icon: "squad" },
  { label: "Tactics", href: "/tactics", icon: "tactics" },
  { label: "My Games", href: "/games", icon: "calendar", match: ["/matches", "/play"] },
  { label: "Training", href: "/training", icon: "training" },
  { label: "Coaches", href: "/coaches", icon: "coach" },
  { label: "Transfer", href: "/transfer", icon: "transfer" },
];
export const COMMUNITY: NavItem[] = [
  { label: "Rules", href: "/rules", icon: "rules" },
  { label: "Forum", href: "/forum", icon: "forum" },
];

const isActive = (path: string, item: NavItem) =>
  [item.href, ...(item.match ?? [])].some((p) => path === p || path.startsWith(`${p}/`));

/** Fades with the sidebar width; keeps its space so nothing moves when the rail opens or closes. */
const fade = (narrow?: boolean) =>
  `transition-opacity duration-200 ${narrow ? "pointer-events-none opacity-0" : "opacity-100"}`;

function NavLink({
  item,
  path,
  count,
  narrow,
  onNavigate,
}: {
  item: NavItem;
  path: string;
  count?: string | null;
  narrow?: boolean;
  onNavigate?: () => void;
}) {
  const active = isActive(path, item);
  return (
    <Link
      href={item.href}
      // Sidebar tabs prefetch the whole page (not only its loading shell), so a click shows it at once.
      prefetch
      onClick={onNavigate}
      aria-current={active ? "page" : undefined}
      className={`group relative flex h-11 items-center gap-3 overflow-hidden rounded-[10px] pr-3 pl-3 text-sm whitespace-nowrap transition-colors duration-[120ms] ${
        active
          ? "bg-link/16 font-semibold text-text"
          : "font-medium text-muted hover:bg-panel hover:text-text"
      }`}
    >
      {active && (
        <span className="absolute top-3 left-0 h-5 w-[3px] rounded-full bg-link" aria-hidden />
      )}
      <AppIcon name={item.icon} className={active ? "text-link" : ""} />
      <span className={`flex-1 ${fade(narrow)}`}>{item.label}</span>
      {count && (
        <>
          <span
            className={`rounded-full bg-foul px-[7px] py-0.5 text-[11px] font-bold text-white ${fade(narrow)}`}
          >
            {count}
          </span>
          <span
            aria-hidden
            className={`absolute top-2 left-[30px] size-2 rounded-full bg-foul transition-opacity duration-200 ${narrow ? "opacity-100" : "opacity-0"}`}
          />
        </>
      )}
    </Link>
  );
}

/**
 * The sidebar content. One layout for every width: 16px padding, a 44px crest and 12px item padding put each icon on
 * the centre line of the 76px rail, so opening or closing the rail only fades the labels (no jumps).
 */
function SidebarBody({
  data,
  path,
  narrow,
  collapsed,
  onNavigate,
  onToggle,
}: {
  data: ShellData;
  path: string;
  /** Showing as the 76px icon rail right now. */
  narrow?: boolean;
  /** The manager collapsed the sidebar (it may be open on hover). */
  collapsed?: boolean;
  onNavigate?: () => void;
  /** Desktop only: collapse to the icon rail / keep it open. */
  onToggle?: () => void;
}) {
  const offers = badge(data.counts.offers);
  const label = (text: string) => (
    <p
      className={`mb-1.5 pl-1 text-[11px] font-bold tracking-[0.08em] whitespace-nowrap text-muted ${fade(narrow)}`}
    >
      {text}
    </p>
  );
  return (
    <div className="relative flex h-full w-full flex-col gap-1 px-4 py-5">
      <Link
        href="/squad"
        onClick={onNavigate}
        className="flex items-center gap-3 pb-5"
        aria-label="Your club"
      >
        <Crest kit={data.club?.kit} initials={initials(data.club?.name ?? "SV")} size={44} />
        <span className={`min-w-0 pr-8 ${fade(narrow)}`}>
          <span className="block truncate text-[15px] font-bold whitespace-nowrap text-text">
            {data.club?.name ?? "No club yet"}
          </span>
          <span className="block text-xs whitespace-nowrap text-muted">
            {data.club?.meta ?? "Create your team"}
          </span>
        </span>
      </Link>
      {onToggle && (
        <button
          type="button"
          onClick={onToggle}
          tabIndex={narrow ? -1 : 0}
          aria-label={collapsed ? "Keep sidebar open" : "Collapse sidebar"}
          className={`absolute top-[30px] right-2 hidden size-8 items-center justify-center rounded-lg text-muted transition-colors duration-[120ms] hover:bg-panel hover:text-text xl:flex ${fade(narrow)}`}
        >
          <AppIcon name={collapsed ? "chevronRight" : "chevronLeft"} size={18} />
        </button>
      )}
      <span className="mb-3 h-px bg-divider" />
      {label("MENU")}
      <nav aria-label="Main" className="flex flex-col gap-1">
        {NAV.map((item) => (
          <NavLink
            key={item.href}
            item={item}
            path={path}
            narrow={narrow}
            onNavigate={onNavigate}
            count={item.href === "/transfer" ? offers : null}
          />
        ))}
      </nav>
      <span className="flex-1" />
      {label("COMMUNITY")}
      <nav aria-label="Community" className="flex flex-col gap-1">
        {COMMUNITY.map((item) => (
          <NavLink
            key={item.href}
            item={item}
            path={path}
            narrow={narrow}
            onNavigate={onNavigate}
          />
        ))}
      </nav>
    </div>
  );
}

const NOTE_ICON: Record<ShellNotification["kind"], [IconName, string]> = {
  match: ["trophy", "text-chance bg-chance/16"],
  transfer: ["transfer", "text-info bg-info/16"],
  training: ["training", "text-link bg-link/16"],
  injury: ["alert", "text-foul bg-foul/16"],
  finance: ["wallet", "text-goal bg-goal/16"],
  club: ["home", "text-link bg-link/16"],
  system: ["ball", "text-link bg-link/16"],
};

type Panel = "notifications" | "messages" | "profile" | null;

function useOutside(ref: React.RefObject<HTMLElement | null>, onOut: () => void, active: boolean) {
  useEffect(() => {
    if (!active) return;
    const click = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) onOut();
    };
    const key = (e: KeyboardEvent) => {
      if (e.key === "Escape") onOut();
    };
    document.addEventListener("mousedown", click);
    document.addEventListener("keydown", key);
    return () => {
      document.removeEventListener("mousedown", click);
      document.removeEventListener("keydown", key);
    };
  }, [ref, onOut, active]);
}

function Dropdown({
  title,
  action,
  children,
  footer,
  width = "w-[400px]",
}: {
  title: string;
  action?: React.ReactNode;
  children: React.ReactNode;
  footer?: { href: string; label: string };
  width?: string;
}) {
  return (
    <div
      role="dialog"
      aria-label={title}
      className={`absolute top-[52px] right-0 z-40 ${width} max-w-[calc(100vw-24px)] origin-top-right animate-sv-pop overflow-hidden rounded-[14px] border border-border bg-panel-2 shadow-[0_16px_40px_rgb(0_0_0/0.5)]`}
    >
      <div className="flex items-center gap-2 border-b border-divider px-[18px] py-3.5">
        <h2 className="flex-1 text-[15px] font-bold text-text">{title}</h2>
        {action}
      </div>
      <div className="max-h-[420px] overflow-y-auto">{children}</div>
      {footer && (
        <Link
          href={footer.href}
          className="block border-t border-divider py-3 text-center text-[13px] font-semibold text-link hover:underline"
        >
          {footer.label}
        </Link>
      )}
    </div>
  );
}

function IconButton({
  icon,
  label,
  count,
  open,
  onClick,
}: {
  icon: IconName;
  label: string;
  count: string | null;
  open: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-expanded={open}
      aria-label={count ? `${label} (${count} unread)` : label}
      className={`relative flex size-10 items-center justify-center rounded-[10px] text-text transition-colors ${open ? "bg-border" : "bg-panel hover:bg-border"}`}
    >
      <AppIcon name={icon} />
      {count && (
        <span className="absolute top-0.5 left-6 rounded-full bg-foul px-[5px] text-[10px] leading-4 font-bold text-white ring-2 ring-panel-2">
          {count}
        </span>
      )}
    </button>
  );
}

/** Tailwind `xl`: the width where the sidebar can show labels. */
const XL = "(min-width: 1280px)";
const subscribeXl = (cb: () => void) => {
  const q = window.matchMedia(XL);
  q.addEventListener("change", cb);
  return () => q.removeEventListener("change", cb);
};
const readXl = () => window.matchMedia(XL).matches;

/** Desktop sidebar: "collapsed" keeps the icon rail (per browser). */
const SIDEBAR_KEY = "sv-sidebar";
const subscribeSidebar = (cb: () => void) => {
  window.addEventListener("storage", cb);
  return () => window.removeEventListener("storage", cb);
};
const readSidebar = () => {
  try {
    return localStorage.getItem(SIDEBAR_KEY) === "collapsed";
  } catch {
    return false;
  }
};

const SCOPE_KEY = "sv-scope";
const subscribeScope = (cb: () => void) => {
  window.addEventListener("storage", cb);
  return () => window.removeEventListener("storage", cb);
};
const readScope = () => {
  try {
    return localStorage.getItem(SCOPE_KEY) === "league" ? "league" : "world";
  } catch {
    return "world";
  }
};

function TopBar({ data, onMenu }: { data: ShellData; onMenu: () => void }) {
  const [panel, setPanel] = useState<Panel>(null);
  const [confirmOut, setConfirmOut] = useState(false);
  const [readPending, startRead] = useTransition();
  const ref = useRef<HTMLDivElement>(null);
  const router = useRouter();
  const scope = useSyncExternalStore(subscribeScope, readScope, () => "world" as const);
  const searchRef = useRef<HTMLInputElement>(null);
  useOutside(ref, () => setPanel(null), panel !== null);
  const toggle = (p: Panel) => setPanel((cur) => (cur === p ? null : p));
  const setScope = (s: "world" | "league") => {
    try {
      localStorage.setItem(SCOPE_KEY, s);
      window.dispatchEvent(new StorageEvent("storage"));
    } catch {}
  };

  // "/" focuses search (Figma: shortcut hint in the field).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const t = e.target as HTMLElement | null;
      if (e.key === "/" && t?.tagName !== "INPUT" && t?.tagName !== "TEXTAREA") {
        e.preventDefault();
        searchRef.current?.focus();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  const n = badge(data.counts.notifications);
  const m = badge(data.counts.messages);
  return (
    <header className="sticky top-0 z-30 flex h-16 items-center gap-3 border-b border-divider bg-panel px-4 lg:gap-4 lg:px-6">
      <button
        type="button"
        onClick={onMenu}
        className="flex size-10 items-center justify-center rounded-[10px] text-text hover:bg-panel-2 lg:hidden"
        aria-label="Open menu"
      >
        <AppIcon name="menu" size={22} />
      </button>
      <Link href="/home" aria-label="Soccer Vision home" className="shrink-0">
        <Image src={logo} alt="" priority className="h-7 w-auto" />
      </Link>
      <div
        role="group"
        aria-label="Search scope"
        className="hidden items-center gap-1 rounded-[10px] bg-panel-2 p-1 md:flex"
      >
        {(["world", "league"] as const).map((s) => (
          <button
            key={s}
            type="button"
            aria-pressed={scope === s}
            onClick={() => setScope(s)}
            className={`rounded-lg px-3.5 py-1.5 text-[13px] transition-colors duration-200 ${
              scope === s
                ? "bg-border font-semibold text-text"
                : "font-medium text-muted hover:text-text"
            }`}
          >
            {s === "world" ? "World" : "My League"}
          </button>
        ))}
      </div>
      <span className="flex-1" />
      <form
        role="search"
        className="hidden h-10 w-[380px] items-center gap-2.5 rounded-[10px] border border-border bg-panel-2 px-3.5 focus-within:border-link xl:flex"
        onSubmit={(e) => {
          e.preventDefault();
          const q = new FormData(e.currentTarget).get("q");
          if (typeof q === "string" && q.trim())
            router.push(`/search?q=${encodeURIComponent(q.trim())}&scope=${scope}`);
        }}
      >
        <AppIcon name="search" size={18} className="text-muted" />
        <input
          ref={searchRef}
          name="q"
          type="search"
          aria-label="Search teams, players or user ID"
          placeholder={
            scope === "league"
              ? "Search clubs and players in your league"
              : "Search teams, players or user ID"
          }
          className="min-w-0 flex-1 bg-transparent text-[13px] text-text outline-none placeholder:text-muted"
        />
        <kbd className="rounded border border-border px-1.5 text-[11px] font-semibold text-muted">
          /
        </kbd>
      </form>
      <Link
        href="/search"
        aria-label="Search"
        className="flex size-10 items-center justify-center rounded-[10px] bg-panel text-text hover:bg-border xl:hidden"
      >
        <AppIcon name="search" />
      </Link>
      <div ref={ref} className="relative flex items-center gap-2 lg:gap-3">
        <IconButton
          icon="bell"
          label="Notifications"
          count={n}
          open={panel === "notifications"}
          onClick={() => toggle("notifications")}
        />
        <IconButton
          icon="mail"
          label="Messages"
          count={m}
          open={panel === "messages"}
          onClick={() => toggle("messages")}
        />
        <span className="hidden h-7 w-px bg-divider sm:block" />
        <button
          type="button"
          onClick={() => toggle("profile")}
          aria-expanded={panel === "profile"}
          aria-label="Account menu"
          className="flex items-center gap-2.5 rounded-[10px] py-1 pr-1 hover:bg-panel-2"
        >
          <span className="flex size-9 items-center justify-center rounded-full bg-link text-[13px] font-bold text-white">
            {initials(data.manager.username ?? data.manager.name)}
          </span>
          <span className="hidden text-left sm:block">
            <span className="block text-sm font-semibold text-text" data-testid="user-name">
              {data.manager.username ?? data.manager.name}
            </span>
            <span className="block text-[11px] text-muted">Manager</span>
          </span>
          <AppIcon name="chevronDown" size={16} className="hidden text-muted sm:block" />
        </button>

        {panel === "notifications" && (
          <Dropdown
            title="Notifications"
            footer={{ href: "/notifications", label: "See all notifications" }}
            action={
              n ? (
                <button
                  type="button"
                  disabled={readPending}
                  onClick={() => startRead(() => markNotificationsReadAction())}
                  className="text-xs font-semibold text-link hover:underline disabled:opacity-50"
                >
                  Mark all as read
                </button>
              ) : null
            }
          >
            {data.notifications.length === 0 ? (
              <p className="px-[18px] py-8 text-center text-[13px] text-muted">
                No notifications yet. Match results, training reports and transfer offers show up
                here.
              </p>
            ) : (
              <ul>
                {data.notifications.map((note) => {
                  const [icon, tone] = NOTE_ICON[note.kind];
                  const body = (
                    <>
                      <span
                        className={`flex size-9 shrink-0 items-center justify-center rounded-full ${tone}`}
                      >
                        <AppIcon name={icon} size={18} />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block text-[13px] font-semibold text-text">
                          {note.title}
                        </span>
                        {note.body && <span className="block text-xs text-muted">{note.body}</span>}
                      </span>
                      <span className="flex flex-col items-end gap-1.5 text-[11px] text-muted">
                        {relativeTime(note.createdAt)}
                        {note.unread && (
                          <span className="size-2 rounded-full bg-link" aria-label="Unread" />
                        )}
                      </span>
                    </>
                  );
                  const cls = `flex gap-3 px-[18px] py-3 ${note.unread ? "bg-link/10" : ""}`;
                  return (
                    <li key={note.id}>
                      {note.href ? (
                        <Link
                          href={note.href}
                          className={`${cls} hover:bg-panel`}
                          onClick={() => setPanel(null)}
                        >
                          {body}
                        </Link>
                      ) : (
                        <div className={cls}>{body}</div>
                      )}
                    </li>
                  );
                })}
              </ul>
            )}
          </Dropdown>
        )}

        {panel === "messages" && (
          <Dropdown
            title="Messages"
            width="w-[380px]"
            footer={{ href: "/messages", label: "Open inbox" }}
          >
            {data.messages.length === 0 ? (
              <p className="px-[18px] py-8 text-center text-[13px] text-muted">
                No messages yet. Managers you play online can message you here.
              </p>
            ) : (
              <ul>
                {data.messages.map((msg) => (
                  <li
                    key={msg.id}
                    className={`flex items-center gap-3 px-[18px] py-3 ${msg.unread ? "bg-link/10" : ""}`}
                  >
                    <span className="flex size-[38px] shrink-0 items-center justify-center rounded-full bg-info text-[13px] font-bold text-white">
                      {initials(msg.from)}
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="flex gap-2">
                        <span
                          className={`flex-1 truncate text-[13px] text-text ${msg.unread ? "font-bold" : "font-semibold"}`}
                        >
                          {msg.from}
                        </span>
                        <span className="text-[11px] text-muted">
                          {relativeTime(msg.createdAt)}
                        </span>
                      </span>
                      <span
                        className={`block truncate text-xs ${msg.unread ? "text-text" : "text-muted"}`}
                      >
                        {msg.body}
                      </span>
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </Dropdown>
        )}

        {panel === "profile" && (
          <Dropdown title="Account" width="w-[280px]">
            <div className="flex items-center gap-3 border-b border-divider px-[18px] py-4">
              <span className="flex size-11 items-center justify-center rounded-full bg-link text-[15px] font-bold text-white">
                {initials(data.manager.username ?? data.manager.name)}
              </span>
              <span className="min-w-0">
                <span className="block truncate text-[15px] font-bold text-text">
                  {data.manager.username ?? data.manager.name}
                </span>
                <span className="block truncate text-xs text-muted">{data.manager.email}</span>
              </span>
            </div>
            {(
              [
                ["home", "Club profile", "/squad"],
                ["settings", "Settings", "/settings"],
                ["rules", "Help & rules", "/rules"],
              ] as [IconName, string, string][]
            ).map(([icon, label, href]) => (
              <Link
                key={href}
                href={href}
                onClick={() => setPanel(null)}
                className="flex items-center gap-3 px-[18px] py-2.5 text-sm font-medium text-text hover:bg-panel"
              >
                <AppIcon name={icon} size={18} className="text-muted" />
                {label}
              </Link>
            ))}
            <button
              type="button"
              onClick={() => {
                setPanel(null);
                setConfirmOut(true);
              }}
              className="flex w-full items-center gap-3 border-t border-divider px-[18px] py-2.5 text-sm font-medium text-foul hover:bg-panel"
            >
              <AppIcon name="logout" size={18} />
              Log out
            </button>
          </Dropdown>
        )}
      </div>
      {confirmOut && <LogOutDialog onCancel={() => setConfirmOut(false)} />}
    </header>
  );
}

function LogOutDialog({ onCancel }: { onCancel: () => void }) {
  const cancelRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    cancelRef.current?.focus();
    const key = (e: KeyboardEvent) => e.key === "Escape" && onCancel();
    document.addEventListener("keydown", key);
    return () => document.removeEventListener("keydown", key);
  }, [onCancel]);
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <button
        type="button"
        aria-label="Close"
        onClick={onCancel}
        className="absolute inset-0 animate-sv-scrim bg-bg/75"
      />
      <div
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="logout-title"
        className="relative flex w-full max-w-[420px] animate-sv-dialog flex-col gap-3.5 rounded-2xl border border-border bg-panel-2 p-7 shadow-[0_20px_48px_rgb(0_0_0/0.5)]"
      >
        <div className="flex items-center gap-3">
          <span className="flex size-10 items-center justify-center rounded-full bg-foul/16 text-foul">
            <AppIcon name="logout" />
          </span>
          <h2 id="logout-title" className="text-[19px] font-extrabold text-text">
            Log out of Soccer Vision?
          </h2>
        </div>
        <p className="text-sm leading-[21px] text-muted">
          Your club keeps its saved lineup and tactics while you are away.
        </p>
        <form action={signOutAction} className="flex gap-2.5">
          <button
            ref={cancelRef}
            type="button"
            onClick={onCancel}
            className="h-11 flex-1 rounded-lg border border-border bg-panel-2 text-[15px] font-semibold text-text hover:bg-panel"
          >
            Cancel
          </button>
          <button
            type="submit"
            className="h-11 flex-1 rounded-lg bg-foul text-[15px] font-semibold text-white hover:brightness-110"
          >
            Sign out
          </button>
        </form>
      </div>
    </div>
  );
}

const subscribeOnline = (cb: () => void) => {
  window.addEventListener("online", cb);
  window.addEventListener("offline", cb);
  return () => {
    window.removeEventListener("online", cb);
    window.removeEventListener("offline", cb);
  };
};

/** "Connection lost" banner under the top bar (Figma: Home – partial error / offline). */
function OfflineBanner() {
  const online = useSyncExternalStore(
    subscribeOnline,
    () => navigator.onLine,
    () => true,
  );
  if (online) return null;
  return (
    <div
      role="status"
      className="flex animate-sv-pop items-center gap-2.5 bg-chance/16 px-4 py-2.5 text-[13px] font-medium text-text lg:px-7"
    >
      <AppIcon name="alert" size={18} className="text-chance" />
      Connection lost — showing the last data we loaded. We reconnect automatically.
    </div>
  );
}

/** The signed-in app frame: sidebar (icon rail on tablets, a drawer on phones), top bar, page. */
export function ShellClient({
  data,
  skipLink,
  children,
}: {
  data: ShellData;
  /** Rendered first, before the sidebar (keyboard: skip to the content). */
  skipLink?: React.ReactNode;
  children: React.ReactNode;
}) {
  const path = usePathname();
  const [drawer, setDrawer] = useState(false);
  const collapsed = useSyncExternalStore(subscribeSidebar, readSidebar, () => false);
  // Collapsed on desktop: hovering the rail slides the full sidebar open over the page.
  const [peek, setPeek] = useState(false);
  const wide = !collapsed || peek;
  const isXl = useSyncExternalStore(subscribeXl, readXl, () => true);
  const toggle = () => {
    try {
      localStorage.setItem(SIDEBAR_KEY, collapsed ? "open" : "collapsed");
      window.dispatchEvent(new StorageEvent("storage"));
    } catch {}
  };
  const [lastPath, setLastPath] = useState(path);
  if (lastPath !== path) {
    setLastPath(path);
    setDrawer(false);
  }
  return (
    <div className="flex min-h-dvh bg-bg text-text">
      {skipLink}
      <div
        className={`sticky top-0 z-40 hidden h-dvh w-[76px] shrink-0 transition-[width] duration-200 ease-[cubic-bezier(.2,0,0,1)] md:block ${collapsed ? "" : "xl:w-[232px]"}`}
      >
        <aside
          onMouseEnter={() => collapsed && setPeek(true)}
          onMouseLeave={() => setPeek(false)}
          className={`absolute inset-y-0 left-0 w-[76px] overflow-x-hidden overflow-y-auto border-r border-divider bg-panel-2 transition-[width,box-shadow] duration-200 ease-[cubic-bezier(.2,0,0,1)] ${
            wide ? "xl:w-[232px]" : ""
          } ${collapsed && peek ? "xl:shadow-[8px_0_32px_rgb(0_0_0/0.45)]" : ""}`}
        >
          <SidebarBody
            data={data}
            path={path}
            narrow={!(isXl && wide)}
            collapsed={collapsed}
            onToggle={toggle}
          />
        </aside>
      </div>
      {drawer && (
        <div className="fixed inset-0 z-50 md:hidden">
          <button
            type="button"
            aria-label="Close menu"
            onClick={() => setDrawer(false)}
            className="absolute inset-0 animate-sv-scrim bg-[var(--mor-black)]/60"
          />
          <aside
            role="dialog"
            aria-modal="true"
            aria-label="Menu"
            className="absolute inset-y-0 left-0 w-[300px] animate-sv-drawer overflow-y-auto bg-panel-2"
          >
            <SidebarBody data={data} path={path} onNavigate={() => setDrawer(false)} />
          </aside>
        </div>
      )}
      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar data={data} onMenu={() => setDrawer(true)} />
        <OfflineBanner />
        <main id="main" className="flex flex-1 flex-col gap-5 p-4 pb-24 md:p-7">
          {children}
        </main>
        <MobileTabs path={path} />
      </div>
    </div>
  );
}

/** Bottom tab bar on phones (Figma: Mobile – home). */
function MobileTabs({ path }: { path: string }) {
  const tabs = [NAV[0]!, NAV[1]!, NAV[2]!, NAV[3]!];
  return (
    <nav
      aria-label="Quick"
      className="fixed inset-x-0 bottom-0 z-30 flex border-t border-divider bg-panel px-2 pt-2 pb-[max(env(safe-area-inset-bottom),12px)] md:hidden"
    >
      {tabs.map((item) => {
        const active = isActive(path, item);
        return (
          <Link
            key={item.href}
            href={item.href}
            aria-current={active ? "page" : undefined}
            className={`flex flex-1 flex-col items-center gap-1 py-1 text-[11px] ${active ? "font-semibold text-link" : "font-medium text-muted"}`}
          >
            <AppIcon name={item.icon} size={22} />
            {item.label === "My Games" ? "Games" : item.label}
          </Link>
        );
      })}
    </nav>
  );
}
