export function dispatchPartySelected(elementId, detail) {
  const host = document.getElementById(elementId);
  if (!host) {
    return;
  }

  host.dispatchEvent(new CustomEvent("party-selected", {
    bubbles: true,
    composed: true,
    detail
  }));
}
