"use client";

import { useActionState } from "react";

import { createThreadAction, replyAction } from "./actions";

const field =
  "w-full rounded-lg border border-border bg-panel px-3 py-2.5 text-sm text-text placeholder:text-muted focus:border-link focus:outline-none";
const submit =
  "inline-flex h-10 items-center justify-center rounded-lg bg-button px-5 text-sm font-semibold text-button-text hover:bg-border disabled:opacity-50";

export function NewThreadForm() {
  const [state, action, pending] = useActionState(createThreadAction, undefined);
  return (
    <form action={action} className="flex flex-col gap-3">
      <div className="flex flex-col gap-3 sm:flex-row">
        <select
          name="category"
          aria-label="Category"
          className={`${field} sm:w-44`}
          defaultValue="general"
        >
          <option value="general">General</option>
          <option value="tactics">Tactics</option>
          <option value="transfers">Transfers</option>
          <option value="friendlies">Friendlies</option>
          <option value="bugs">Bug reports</option>
        </select>
        <input
          name="title"
          aria-label="Title"
          placeholder="Thread title"
          maxLength={120}
          className={field}
        />
      </div>
      <textarea
        name="body"
        aria-label="First post"
        placeholder="What do you want to talk about?"
        rows={4}
        maxLength={5000}
        className={field}
      />
      {state?.error && (
        <p role="alert" className="text-sm text-foul">
          {state.error}
        </p>
      )}
      <button type="submit" disabled={pending} className={`${submit} self-start`}>
        {pending ? "Posting…" : "Start thread"}
      </button>
    </form>
  );
}

export function ReplyForm({ threadId }: { threadId: string }) {
  const [state, action, pending] = useActionState(replyAction, undefined);
  return (
    <form action={action} className="flex flex-col gap-3">
      <input type="hidden" name="threadId" value={threadId} />
      <textarea
        name="body"
        aria-label="Reply"
        placeholder="Write a reply"
        rows={3}
        maxLength={5000}
        className={field}
      />
      {state?.error && (
        <p role="alert" className="text-sm text-foul">
          {state.error}
        </p>
      )}
      <button type="submit" disabled={pending} className={`${submit} self-start`}>
        {pending ? "Posting…" : "Reply"}
      </button>
    </form>
  );
}
