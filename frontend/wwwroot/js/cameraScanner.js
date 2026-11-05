// Camera QR & Barcode Scanner for POS System using Html5Qrcode
window.posCameraScanner = {
    html5QrCode: null,
    dotNetHelper: null,
    isScanning: false,

    start: async function (dotNetHelper, elementId) {
        this.dotNetHelper = dotNetHelper;
        const container = document.getElementById(elementId);
        if (!container) {
            console.warn("Camera container element not found:", elementId);
            return false;
        }

        // Tắt scanner cũ nếu đang chạy
        await this.stop();

        if (typeof Html5Qrcode === "undefined") {
            console.warn("Html5Qrcode library not loaded yet.");
            return false;
        }

        try {
            this.html5QrCode = new Html5Qrcode(elementId, { verbose: false });
            const config = {
                fps: 15,
                qrbox: function(viewfinderWidth, viewfinderHeight) {
                    const minEdgePercentage = 0.70;
                    const minEdgeSize = Math.min(viewfinderWidth, viewfinderHeight);
                    const qrboxSize = Math.floor(minEdgeSize * minEdgePercentage);
                    return {
                        width: qrboxSize,
                        height: qrboxSize
                    };
                },
                aspectRatio: 1.0,
                experimentalFeatures: {
                    useBarCodeDetectorIfSupported: true
                }
            };

            await this.html5QrCode.start(
                { facingMode: "environment" }, // Ưu tiên camera sau trên điện thoại
                config,
                (decodedText, decodedResult) => {
                    if (this.isScanning) {
                        this.isScanning = false;
                        if (this.dotNetHelper) {
                            this.dotNetHelper.invokeMethodAsync('OnCodeScannedFromCamera', decodedText);
                        }
                    }
                },
                (errorMessage) => {
                    // Đang quét từng khung hình, bỏ qua frame trống
                }
            );

            this.isScanning = true;
            return true;
        } catch (err) {
            console.warn("Could not start camera scanner:", err);
            this.isScanning = false;
            return false;
        }
    },

    stop: async function () {
        this.isScanning = false;
        if (this.html5QrCode) {
            try {
                if (this.html5QrCode.isScanning) {
                    await this.html5QrCode.stop();
                }
                this.html5QrCode.clear();
            } catch (err) {
                console.warn("Error while stopping camera:", err);
            } finally {
                this.html5QrCode = null;
            }
        }
    }
};

// Hàm in chuyên dụng cho Tem mã QR và Hóa đơn nhiệt K80
window.printThermalElement = function (elementId) {
    try {
        const el = document.getElementById(elementId);
        if (!el) {
            window.print();
            return;
        }

        let printFrame = document.getElementById("pos-print-iframe");
        if (printFrame) {
            printFrame.remove();
        }
        printFrame = document.createElement("iframe");
        printFrame.id = "pos-print-iframe";
        printFrame.style.position = "fixed";
        printFrame.style.right = "0";
        printFrame.style.bottom = "0";
        printFrame.style.width = "0";
        printFrame.style.height = "0";
        printFrame.style.border = "0";
        document.body.appendChild(printFrame);

        const doc = printFrame.contentWindow.document;
        doc.open();
        doc.write(`<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <title>In Phiếu QF Fashion</title>
    <style>
        @page { margin: 3mm; size: auto; }
        body { margin: 0; padding: 4px; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #000; background: #fff; }
        * { box-sizing: border-box; }
        .qr-sticker-card {
            padding: 12px;
            border: 1px dashed #333;
            border-radius: 6px;
            text-align: center;
            max-width: 260px;
            margin: 0 auto;
            background: #fff;
        }
        .sticker-brand { font-size: 11px; font-weight: 800; letter-spacing: 0.5px; color: #000; }
        .sticker-prod-name { font-size: 13px; font-weight: 700; margin: 4px 0; color: #000; }
        .sticker-meta { font-size: 11px; color: #333; margin-bottom: 6px; }
        .sticker-qr-image img { width: 140px; height: 140px; }
        .sticker-sku { font-family: monospace; font-size: 12px; font-weight: 700; margin-top: 4px; }
        .sticker-price { font-size: 14px; font-weight: 800; margin-top: 4px; }
        /* Hóa đơn K80 */
        .pos-receipt-k80 { width: 78mm; max-width: 100%; margin: 0 auto; font-size: 12px; font-family: 'Courier New', Courier, monospace; }
        .k80-header { text-align: center; margin-bottom: 8px; }
        .store-brand { font-size: 16px; font-weight: 900; }
        .store-tagline { font-size: 10px; }
        .store-info { font-size: 11px; }
        .receipt-divider, .receipt-divider-thin { text-align: center; font-weight: bold; overflow: hidden; }
        .receipt-title { font-size: 14px; font-weight: bold; margin: 4px 0; }
        .receipt-code { font-size: 11px; }
        .k80-meta { margin: 6px 0; font-size: 11px; }
        .meta-row, .summary-row { display: flex; justify-content: space-between; margin-bottom: 2px; }
        .item-header-row { display: flex; font-weight: bold; margin-bottom: 2px; }
        .item-row { margin-bottom: 4px; }
        .item-name { font-weight: bold; font-size: 11px; }
        .item-sub { display: flex; justify-content: space-between; font-size: 11px; }
        .col-name { flex: 2; text-align: left; }
        .col-qty { width: 28px; text-align: center; }
        .col-price { flex: 1; text-align: right; }
        .col-total { flex: 1.2; text-align: right; }
        .k80-summary { margin-top: 6px; font-size: 11px; }
        .total-row { font-size: 13px; font-weight: 900; border-top: 1px dashed #000; padding-top: 4px; }
        .k80-footer { text-align: center; margin-top: 12px; font-size: 11px; }
    </style>
</head>
<body>
    ${el.outerHTML}
    <script>
        window.onload = function() {
            setTimeout(function() {
                window.focus();
                window.print();
            }, 250);
        };
    <\/script>
</body>
</html>`);
        doc.close();
    } catch (e) {
        console.warn("Print error, fallback to window.print():", e);
        window.print();
    }
};

