/** Saved document text, one numbered row per line. Scrolls when it is long. */
export function DocumentText({ text, label }: { text: string; label: string }) {
  return (
    // tabIndex makes the scrollable area reachable from the keyboard.
    <ol aria-label={label} tabIndex={0} className="document max-h-112 overflow-y-auto">
      {text.split('\n').map((line, index) => (
        <li key={index}>{line}</li>
      ))}
    </ol>
  )
}
