/**
 * Keyboard handler for non-button elements acting as buttons (role="button"):
 * activates on Enter or Space, mirroring native button behavior.
 */
export function keyActivate(handler: () => void) {
  return (e: React.KeyboardEvent) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      handler();
    }
  };
}
