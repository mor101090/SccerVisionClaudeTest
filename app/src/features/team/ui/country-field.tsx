"use client";

import Image from "next/image";
import { useEffect, useId, useMemo, useRef, useState } from "react";

import { flagUrl, type Country } from "../countries";

function Flag({ code }: { code: string }) {
  // flagcdn serves SVG at any size: no optimisation needed.
  return (
    <Image
      src={flagUrl(code)}
      alt=""
      width={24}
      height={18}
      unoptimized
      className="h-[18px] w-6 shrink-0 rounded-[2px] object-cover shadow-[0_0_0_0.5px] shadow-border"
    />
  );
}

/** Lowercase without accents, for search ("Curaçao" matches "curacao"). */
const fold = (s: string) =>
  s
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase();

type Props = {
  label: string;
  name: string;
  countries: Country[];
  defaultValue?: string;
  placeholder?: string;
  helper?: React.ReactNode;
  error?: React.ReactNode;
};

/**
 * A country picker styled as the Figma Text Field: a button showing the picked flag and name, opening a searchable
 * list of flags (a native <select> can't show images). Submits `name` through a hidden input.
 */
export function CountryField({
  label,
  name,
  countries,
  defaultValue,
  placeholder = "Pick your country",
  helper,
  error,
}: Props) {
  const id = useId();
  const [value, setValue] = useState(defaultValue ?? "");
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [active, setActive] = useState(0);
  const root = useRef<HTMLDivElement>(null);
  const button = useRef<HTMLButtonElement>(null);

  const selected = countries.find((c) => c.code === value);
  const shown = useMemo(() => {
    const q = fold(query.trim());
    if (!q) return countries;
    return countries.filter((c) => fold(c.name).includes(q) || c.code.toLowerCase() === q);
  }, [countries, query]);
  const note = error ?? helper;
  const optionId = (code: string) => `${id}-opt-${code}`;

  useEffect(() => {
    if (!open) return;
    const close = (e: MouseEvent) => {
      if (!root.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, [open]);

  useEffect(() => {
    const c = shown[active];
    if (open && c) document.getElementById(optionId(c.code))?.scrollIntoView({ block: "nearest" });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- optionId only depends on the stable useId
  }, [active, open, shown]);

  const openList = () => {
    setQuery("");
    setActive(
      Math.max(
        0,
        countries.findIndex((c) => c.code === value),
      ),
    );
    setOpen(true);
  };
  const pick = (code: string) => {
    setValue(code);
    setOpen(false);
    button.current?.focus();
  };

  const onSearchKey = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "ArrowDown") setActive((i) => Math.min(i + 1, shown.length - 1));
    else if (e.key === "ArrowUp") setActive((i) => Math.max(i - 1, 0));
    else if (e.key === "Home") setActive(0);
    else if (e.key === "End") setActive(shown.length - 1);
    else if (e.key === "Enter") {
      const c = shown[active];
      if (c) pick(c.code);
    } else if (e.key === "Escape") {
      setOpen(false);
      button.current?.focus();
    } else return;
    e.preventDefault();
  };

  return (
    <div ref={root} className="flex flex-col gap-1.5">
      <label
        id={`${id}-label`}
        htmlFor={`${id}-button`}
        className="text-[13px] leading-[1.45] font-medium text-text"
      >
        {label}
      </label>
      <input type="hidden" name={name} value={value} />
      <div className="relative">
        <button
          ref={button}
          id={`${id}-button`}
          type="button"
          aria-labelledby={`${id}-label ${id}-button`}
          aria-haspopup="listbox"
          aria-expanded={open}
          aria-controls={`${id}-list`}
          aria-describedby={note ? `${id}-note` : undefined}
          onClick={() => (open ? setOpen(false) : openList())}
          onKeyDown={(e) => {
            if (!open && (e.key === "ArrowDown" || e.key === "ArrowUp")) {
              e.preventDefault();
              openList();
            }
          }}
          className={`flex h-11 w-full items-center gap-2.5 rounded-lg border bg-white px-3 text-left text-[15px] text-field-ink outline-none focus:border-2 focus:shadow-[0_0_0_3px] ${
            error
              ? "border-foul focus:shadow-foul/25"
              : "border-border focus:border-link focus:shadow-link/25"
          }`}
        >
          {selected ? (
            <>
              <Flag code={selected.code} />
              <span className="truncate">{selected.name}</span>
            </>
          ) : (
            <span className="text-muted">{placeholder}</span>
          )}
          <svg
            aria-hidden
            viewBox="0 0 20 20"
            className="ml-auto size-4 shrink-0 text-muted"
            fill="currentColor"
          >
            <path d="M5.2 7.5a.75.75 0 0 1 1.06 0L10 11.24l3.74-3.74a.75.75 0 1 1 1.06 1.06l-4.27 4.27a.75.75 0 0 1-1.06 0L5.2 8.56a.75.75 0 0 1 0-1.06Z" />
          </svg>
        </button>

        {open && (
          <div className="absolute inset-x-0 top-full z-20 mt-1 overflow-hidden rounded-lg border border-border bg-white shadow-lg">
            <input
              // the search box drives the list (WAI-ARIA combobox with a listbox popup)
              role="combobox"
              aria-label={`Search ${label.toLowerCase()}`}
              aria-expanded
              aria-controls={`${id}-list`}
              aria-autocomplete="list"
              aria-activedescendant={shown[active] ? optionId(shown[active].code) : undefined}
              autoFocus
              value={query}
              onChange={(e) => {
                setQuery(e.target.value);
                setActive(0);
              }}
              onKeyDown={onSearchKey}
              placeholder="Search"
              className="h-10 w-full border-b border-border px-3 text-[15px] text-field-ink outline-none placeholder:text-muted"
            />
            <ul
              id={`${id}-list`}
              role="listbox"
              aria-labelledby={`${id}-label`}
              className="max-h-64 overflow-y-auto py-1"
            >
              {shown.map((c, i) => (
                <li
                  key={c.code}
                  id={optionId(c.code)}
                  role="option"
                  aria-selected={c.code === value}
                  onMouseDown={(e) => e.preventDefault()}
                  onMouseMove={() => setActive(i)}
                  onClick={() => pick(c.code)}
                  className={`flex cursor-pointer items-center gap-2.5 px-3 py-1.5 text-[15px] text-field-ink ${
                    i === active ? "bg-link/10" : ""
                  } ${c.code === value ? "font-semibold" : ""}`}
                >
                  <Flag code={c.code} />
                  <span className="truncate">{c.name}</span>
                </li>
              ))}
              {!shown.length && <li className="px-3 py-2 text-sm text-muted">No country found.</li>}
            </ul>
          </div>
        )}
      </div>
      {note && (
        <p
          id={`${id}-note`}
          role={error ? "alert" : undefined}
          className={`text-xs leading-[1.45] ${error ? "text-foul" : "text-muted"}`}
        >
          {note}
        </p>
      )}
    </div>
  );
}
