// Utility functions for report generation
// Ensure functions are attached to window object

(function() {
    'use strict';
    
    window.downloadFile = function (fileName, base64Content, contentType) {
        try {
            // Convert base64 to blob
            const byteCharacters = atob(base64Content);
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
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            window.URL.revokeObjectURL(url);
        } catch (error) {
            console.error('Error downloading file:', error);
            alert('Error downloading file: ' + error.message);
        }
    };

    window.openPrintWindow = function (htmlContent) {
        try {
            const printWindow = window.open('', '_blank');
            if (printWindow) {
                printWindow.document.write(htmlContent);
                printWindow.document.close();
                printWindow.onload = function () {
                    printWindow.print();
                };
            } else {
                alert('Please allow pop-ups to print the report.');
            }
        } catch (error) {
            console.error('Error opening print window:', error);
            alert('Error opening print window: ' + error.message);
        }
    };
})();
