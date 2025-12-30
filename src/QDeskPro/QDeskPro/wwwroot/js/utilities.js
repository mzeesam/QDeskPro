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

// Download a file from byte array
window.downloadFileFromBytes = function (filename, contentType, byteArray) {
    // Create blob from byte array
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
    // Try native share API first (mobile devices)
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
    }

    // Fallback: Try modern clipboard API (requires HTTPS)
    if (navigator.clipboard && navigator.clipboard.writeText) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (err) {
            console.log('Clipboard API failed, using fallback:', err);
        }
    }

    // Final fallback: Works on HTTP (no secure context required)
    // Uses temporary textarea element for copying
    try {
        const textArea = document.createElement('textarea');
        textArea.value = text;

        // Make it invisible but still accessible
        textArea.style.position = 'fixed';
        textArea.style.top = '0';
        textArea.style.left = '0';
        textArea.style.width = '2em';
        textArea.style.height = '2em';
        textArea.style.padding = '0';
        textArea.style.border = 'none';
        textArea.style.outline = 'none';
        textArea.style.boxShadow = 'none';
        textArea.style.background = 'transparent';
        textArea.style.opacity = '0';

        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();

        // Try to copy using execCommand (deprecated but works everywhere)
        const successful = document.execCommand('copy');
        document.body.removeChild(textArea);

        if (successful) {
            return true;
        } else {
            console.error('execCommand copy failed');
            return false;
        }
    } catch (err) {
        console.error('All copy methods failed:', err);
        return false;
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
