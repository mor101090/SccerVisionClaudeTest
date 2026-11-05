/**
 * True when TeamPlan.Parse can read the text without throwing. Unknown keys are ignored, the same way the
 * engine ignores them. A numeric field is rejected only in the shapes the engine actually parses.
 */
const INT = /^-?\d+$/;
const FLOAT = /^-?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?$/;

function isInt(value: string): boolean {
  return INT.test(value);
}

function isFloat(value: string): boolean {
  if (!FLOAT.test(value)) return false;
  return Number.isFinite(Number(value));
}

function listOk(value: string): boolean {
  return value.split(".").every((part) => part.length === 0 || isInt(part));
}

function triplesOk(value: string): boolean {
  for (const entry of value.split("/")) {
    const parts = entry.split(":");
    if (parts.length !== 3) continue;
    if (!isInt(parts[0]!) || !isFloat(parts[1]!) || !isFloat(parts[2]!)) return false;
  }
  return true;
}

const LIST_KEYS = new Set(["ctr", "ctl", "fkr", "fkl", "pen", "czr", "czl"]);
const FLOAT_KEYS = new Set(["line", "width", "tempo", "aggression"]);
const INT_KEYS = new Set(["crr", "crl", "passing", "defence", "attack", "special"]);

export function planTextIsSafe(text: string): boolean {
  for (const part of text.split(";")) {
    const eq = part.indexOf("=");
    if (eq <= 0) continue;
    const key = part.slice(0, eq).trim();
    const value = part.slice(eq + 1).trim();
    if (!value || key === "formation") continue;
    if (key === "slots") {
      const slots = value.split(",");
      if (slots.length === 11 && slots.some((slot) => !isInt(slot))) return false;
      continue;
    }
    if (key === "orders") {
      const orders = value.split(",");
      if (orders.length !== 11) continue;
      for (const order of orders) {
        for (const piece of order.split(".")) {
          if (!isInt(piece)) return false;
        }
      }
      continue;
    }
    if (key === "takers") {
      if (!value.split(",").every(listOk)) return false;
      continue;
    }
    if (LIST_KEYS.has(key)) {
      if (!listOk(value)) return false;
      continue;
    }
    if (key === "pos" || key === "cpa" || key === "cpd") {
      if (!triplesOk(value)) return false;
      continue;
    }
    if (FLOAT_KEYS.has(key) && !isFloat(value)) return false;
    if (INT_KEYS.has(key) && !isInt(value)) return false;
  }
  return true;
}
