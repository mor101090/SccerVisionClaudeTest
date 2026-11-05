/** A text field of a submitted form ("" when missing or a file). */
export function formText(form: FormData, key: string): string {
  const v = form.get(key);
  return typeof v === "string" ? v : "";
}
