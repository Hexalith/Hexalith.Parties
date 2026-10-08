window.HexalithPartiesAdminPortal = window.HexalithPartiesAdminPortal || {};

window.HexalithPartiesAdminPortal.downloadJson = (fileName, contentType, base64Payload) => {
    const bytes = Uint8Array.from(atob(base64Payload), character => character.charCodeAt(0));
    const blob = new Blob([bytes], { type: contentType || "application/json" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    try {
        anchor.href = url;
        anchor.download = fileName;
        document.body.appendChild(anchor);
        anchor.click();
    } finally {
        anchor.remove();
        URL.revokeObjectURL(url);
    }
};
