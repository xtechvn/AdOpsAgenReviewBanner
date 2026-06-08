Tesseract traineddata (tải thủ công):

1. vie.traineddata — tiếng Việt
   https://github.com/tesseract-ocr/tessdata/raw/main/vie.traineddata

2. eng.traineddata — tiếng Anh (fallback)
   https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata

Đặt cả hai file vào thư mục này.
appsettings: Florence:TesseractLanguages = "vie+eng", EnableTesseract = true
