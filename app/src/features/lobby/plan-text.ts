/**
 * True when TeamPlan.Parse can read the text without throwing. Unknown keys are ignored, the same way the
 * engine ignores them. A numeric field is rejected only in the shapes the engine actually parses.
 */
/** A number longer than this cannot be an int or a finite float the engine accepts. Checked before any scan. */
const MAX_NUMBER = 32;

function isDigits(value: string, from: number, to: number): boolean {
  if (from >= to) return false;
  for (let i = from; i < to; i++) {
    const code = value.charCodeAt(i);
    if (code < 48 || code > 57) return false;
  }
  return true;
}

/** True for an invariant integer inside Int32, the range int.Parse accepts. No regular expression. */
function isInt(value: string): boolean {
  if (value.length === 0 || value.length > 11) return false;
  let i = 0;
  if (value[0] === "-" || value[0] === "+") i = 1;
  if (!isDigits(value, i, value.length)) return false;
  const n = Number(value);
  return Number.isInteger(n) && n >= -2_147_483_648 && n <= 2_147_483_647;
}

/**
 * True for the float forms float.Parse accepts: digits, one dot, and an exponent.
 * The shape is a single left-to-right scan, so a long bad field cannot backtrack.
 */
function isFloat(value: string): boolean {
  if (value.length === 0 || value.length > MAX_NUMBER) return false;
  let i = 0;
  if (value[0] === "-" || value[0] === "+") i = 1;
  const body = i;
  let sawDigit = false;
  let sawDot = false;
  for (; i < value.length; i++) {
    const code = value.charCodeAt(i);
    if (code >= 48 && code <= 57) {
      sawDigit = true;
      continue;
    }
    if (code === 46 && !sawDot) {
      sawDot = true;
      continue;
    }
    break;
  }
  if (!sawDigit || i === body) return false;
  if (i < value.length && (value[i] === "e" || value[i] === "E")) {
    i += 1;
    if (value[i] === "+" || value[i] === "-") i += 1;
    const exp = i;
    while (i < value.length && value.charCodeAt(i) >= 48 && value.charCodeAt(i) <= 57) i += 1;
    if (i === exp) return false;
  }
  if (i !== value.length) return false;
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
