import { initializeFluentDropdowns, disposeFluentDropdowns } from "./portal-dropdowns.js";

export { initializeFluentDropdowns };

const pickerBindings = new Map();

export function attachPartyPicker(element, dotNetRef, ownerId) {
  if (!element || !element.isConnected || !dotNetRef) {
    return;
  }

  const owner = ownerId ?? element;
  disposePortalInterop(owner);
  initializeFluentDropdowns(owner, element.closest('.hx-party-form'), () => disposePortalInterop(owner));
  const listener = event => {
    const detail = event.detail ?? {};
    dotNetRef.invokeMethodAsync(
      'OnRelatedPartySelectedAsync',
      detail.partyId ?? null,
      detail.partyType ?? null,
      detail.status ?? null);
  };
  element.addEventListener('party-selected', listener);
  pickerBindings.set(owner, { element, listener });
}

export function disposePortalInterop(ownerId) {
  disposeFluentDropdowns(ownerId);
  const binding = pickerBindings.get(ownerId);
  if (binding) {
    binding.element.removeEventListener('party-selected', binding.listener);
    pickerBindings.delete(ownerId);
  }
}
