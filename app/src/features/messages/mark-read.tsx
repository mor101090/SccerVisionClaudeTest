"use client";

import { useEffect, useRef } from "react";

import { markMessagesReadAction } from "@/features/home/actions";

/** Opening Messages marks the inbox read, so the top bar badge clears. Runs once per visit. */
export function MarkMessagesRead() {
  const done = useRef(false);
  useEffect(() => {
    if (done.current) return;
    done.current = true;
    void markMessagesReadAction();
  }, []);
  return null;
}
