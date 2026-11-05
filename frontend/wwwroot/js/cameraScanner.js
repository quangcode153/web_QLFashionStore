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
