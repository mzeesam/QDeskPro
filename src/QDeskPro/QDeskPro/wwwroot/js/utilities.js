// QDeskPro Utility Functions

// Download a file from base64 data
window.downloadFile = function (filename, contentType, base64Data) {
    // Convert base64 to blob
    const byteCharacters = atob(base64Data);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], { type: contentType });

    // Create download link
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;

    // Trigger download
    document.body.appendChild(link);
    link.click();

    // Cleanup
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
};

// Share text content (for reports)
window.shareText = async function (title, text) {
    if (navigator.share) {
        try {
            await navigator.share({
                title: title,
                text: text
            });
            return true;
        } catch (err) {
            console.log('Share cancelled or failed:', err);
            return false;
        }
    } else {
        // Fallback: copy to clipboard
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (err) {
            console.error('Failed to copy to clipboard:', err);
            return false;
        }
    }
};

// Print content
window.printElement = function (elementId) {
    const printContents = document.getElementById(elementId).innerHTML;
    const originalContents = document.body.innerHTML;

    document.body.innerHTML = printContents;
    window.print();
    document.body.innerHTML = originalContents;
    window.location.reload(); // Reload to restore event handlers
};
