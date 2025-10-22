# WFTradeHelper - Warframe Trade Assistant

`WFTradeHelper` is a C# WPF desktop application designed to assist players of the game *Warframe* with in-game trading. It works by taking a screenshot of the trade window, using Optical Character Recognition (OCR) to identify the prime items, and counting their ducat value.

This project demonstrates the integration of screen capture, OCR, REST API consumption, and a modern desktop UI.

## Features

* **Screen Recognition:** Captures the game's trade window with a single click.
* **OCR Integration:** Uses the **Tesseract** engine to read and parse the names of items from the screenshot.
* **Modern UI:** Built with **WPF** and the **WPF-UI** library for a clean, modern look and feel.
* **Auto Scan:** Primarily used with dual monitor setup for constant scanning your monitor for new items in trade.
* **Overlay:** Displays recognized items and prices in a non-intrusive, always-on-top window.
* **(In Work) Real-Time Pricing:** Fetches current Platinum prices by querying the [Warframe.market](https://warframe.market/) API.
* **(In Work) Trade History:** Saving successful trades and their statistics.
  
## Technical Stack

* **Framework:** .NET 8
* **Desktop UI:** WPF
* **OCR Engine:** [Tesseract](https://github.com/tesseract-ocr/tesseract) (via the `Tesseract` C# wrapper)
* **API & Data:**
    * REST API consumption (`HttpClient`)
    * JSON Deserialization (`Newtonsoft.Json`)
* **Concurrency:** `Async/Await` for non-blocking API calls and UI operations.

## How It Works

1.  Loading existing db of items through items.json in root directory.
2.  The user clicks the "Scan" button or "F8" in the application.
3.  The app takes a small screenshot of the primary display.
4.  The screenshot is processed by the `OCR` service, which uses the Tesseract engine to find and extract text (item names).
5.  Each recognized item name used to evaluate ducat value from db.
6.  The `MainViewModel` updates the UI with the list of items and their corresponding prices.

