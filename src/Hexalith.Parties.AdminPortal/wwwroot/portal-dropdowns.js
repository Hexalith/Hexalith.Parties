const dropdownScopes = new Map();

export function initializeFluentDropdowns(ownerId, element, onDetached) {
  if (!element || !element.isConnected || dropdownScopes.has(ownerId)) {
    return;
  }

  const applySemantics = () => {
    for (const listbox of element.querySelectorAll('fluent-dropdown > fluent-listbox')) {
      listbox.setAttribute('role', 'listbox');
      const dropdown = listbox.closest('fluent-dropdown');
      const labelledBy = dropdown?.getAttribute('aria-labelledby');
      const label = dropdown?.getAttribute('aria-label');
      if (label) {
        listbox.setAttribute('aria-label', label);
      } else if (labelledBy) {
        listbox.setAttribute('aria-labelledby', labelledBy);
      }
    }
    for (const field of element.querySelectorAll('.hx-parties-admin__erasure-dialog fluent-text-input[aria-describedby]')) {
      const input = field.shadowRoot?.querySelector('input');
      const descriptionId = field.getAttribute('aria-describedby');
      const warning = descriptionId ? element.querySelector(`#${CSS.escape(descriptionId)}`) : null;
      if (input && warning) {
        const inputDescriptionId = `${descriptionId}-control`;
        let inputDescription = field.shadowRoot.getElementById(inputDescriptionId);
        if (!inputDescription) {
          inputDescription = document.createElement('span');
          inputDescription.id = inputDescriptionId;
          inputDescription.hidden = true;
          field.shadowRoot.append(inputDescription);
        }
        inputDescription.textContent = warning.textContent.trim();
        input.setAttribute('aria-describedby', inputDescriptionId);
      }
    }
  };

  applySemantics();
  const observer = new MutationObserver(applySemantics);
  observer.observe(element, { childList: true, subtree: true });
  const detachmentObserver = new MutationObserver(() => {
    if (!element.isConnected) {
      disposeFluentDropdowns(ownerId);
      onDetached?.();
    }
  });
  detachmentObserver.observe(document.body, { childList: true, subtree: true });
  dropdownScopes.set(ownerId, { observer, detachmentObserver });
}

export function disposeFluentDropdowns(ownerId) {
  const scope = dropdownScopes.get(ownerId);
  scope?.observer.disconnect();
  scope?.detachmentObserver.disconnect();
  dropdownScopes.delete(ownerId);
}
