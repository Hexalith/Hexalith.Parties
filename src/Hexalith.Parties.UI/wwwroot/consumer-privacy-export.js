window.HexalithPartiesConsumerPortal = window.HexalithPartiesConsumerPortal || {};

window.HexalithPartiesConsumerPortal.downloadJson = async (fileName, contentType, streamReference) => {
    const arrayBuffer = await streamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type: contentType || "application/json" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    try {
        anchor.href = url;
        anchor.download = fileName || "my-data-export.json";
        document.body.appendChild(anchor);
        anchor.click();
    } finally {
        anchor.remove();
        URL.revokeObjectURL(url);
    }
};
