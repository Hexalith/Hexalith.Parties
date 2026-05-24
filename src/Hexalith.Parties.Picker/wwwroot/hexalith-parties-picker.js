export function dispatchPartySelected(elementId, detail) {
  const host = document.getElementById(elementId);
  if (!host) {
    return;
  }

  const safeDetail = {
    partyId: typeof detail?.partyId === "string" ? detail.partyId : null,
    partyType: typeof detail?.partyType === "string" ? detail.partyType : null,
    status: typeof detail?.status === "string" ? detail.status : null
  };

  host.dispatchEvent(new CustomEvent("party-selected", {
    bubbles: true,
    composed: true,
    detail: safeDetail,
  }));
}
