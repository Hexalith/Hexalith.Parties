export function attachPartyPicker(element, dotNetRef) {
  if (!element || !dotNetRef) {
    return;
  }

  element.addEventListener('party-selected', event => {
    const detail = event.detail ?? {};
    dotNetRef.invokeMethodAsync(
      'OnRelatedPartySelectedAsync',
      detail.partyId ?? null,
      detail.partyType ?? null,
      detail.status ?? null);
  });
}
