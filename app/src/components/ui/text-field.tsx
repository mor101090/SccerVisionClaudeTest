"use client";

import { useId, useState } from "react";

import { EyeIcon } from "./icons";

type Props = Omit<React.InputHTMLAttributes<HTMLInputElement>, "className"> & {
  label: string;
  /** Muted helper line under the input (Figma: Text Field / Helper). */
  helper?: React.ReactNode;
  /** Error line; turns the stroke color/foul. */
  error?: React.ReactNode;
  /** Adds the eye toggle for password inputs. */
  revealable?: boolean;
  children?: React.ReactNode;
};

/**
 * Figma "Text Field" (reference: Login): 13px label, 44px white input with a 1px color/border
 * stroke and 8px radius, 15px value in field ink, placeholder color/muted. Focus: link-colored stroke + glow.
 */
export function TextField({
  label,
  helper,
  error,
  revealable,
  disabled,
  type,
  id,
  children,
  ...rest
}: Props) {
  const autoId = useId();
  const inputId = id ?? autoId;
  const noteId = `${inputId}-note`;
  const [shown, setShown] = useState(false);
  const note = error ?? helper;

  return (
    <div className={`flex flex-col gap-1.5 ${disabled ? "opacity-50" : ""}`}>
      <label htmlFor={inputId} className="text-[13px] leading-[1.45] font-medium text-text">
        {label}
      </label>
      <div
        className={`flex h-11 items-center gap-2.5 rounded-lg border bg-white px-3.5 transition-shadow focus-within:border-2 focus-within:px-[13px] focus-within:shadow-[0_0_0_3px] ${
          error
            ? "border-foul focus-within:border-foul focus-within:shadow-foul/25"
            : "border-border focus-within:border-link focus-within:shadow-link/25"
        }`}
      >
        <input
          id={inputId}
          type={revealable ? (shown ? "text" : "password") : type}
          disabled={disabled}
          aria-invalid={error ? true : undefined}
          aria-describedby={note ? noteId : undefined}
          className="h-full min-w-0 flex-1 bg-transparent text-[15px] leading-[1.45] text-field-ink caret-link outline-none placeholder:text-muted disabled:cursor-not-allowed"
          {...rest}
        />
        {revealable && (
          <button
            type="button"
            onClick={() => setShown((s) => !s)}
            aria-label={shown ? "Hide password" : "Show password"}
            aria-pressed={shown}
            className="-mr-1 rounded p-1 text-muted hover:text-field-ink focus-visible:outline-2 focus-visible:outline-link"
          >
            <EyeIcon />
          </button>
        )}
      </div>
      {children}
      {note && (
        <p
          id={noteId}
          role={error ? "alert" : undefined}
          className={`text-xs leading-[1.45] ${error ? "text-foul" : "text-muted"}`}
        >
          {note}
        </p>
      )}
    </div>
  );
}
