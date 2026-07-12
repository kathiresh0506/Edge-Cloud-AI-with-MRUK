# QUANTUMSHIFT-XR

**A Mixed Reality Alien Shooter for Meta Quest 3, powered by distributed AI across Snapdragon devices**

Built at the Snapdragon Multiverse Hackathon Finale — Qualcomm Bangalore Campus, July 2026

<!--
SCREENSHOT 1: Hero/cover image
Type: A single striking gameplay screenshot or short GIF from the Quest 3 (screen recording via
Quest Casting), ideally showing the player in a real room with an alien and the passthrough
environment visible. This is the first thing judges see — pick your best-looking moment.
Place directly below the title, before any text.
-->

![Gameplay Hero Shot](docs/screenshots/hero.png)

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [System Architecture](#system-architecture)
- [Hardware & Software Requirements](#hardware--software-requirements)
- [Project Structure](#project-structure)
- [Setup Guide](#setup-guide)
  - [1. Unity + Meta Quest 3](#1-unity--meta-quest-3)
  - [2. Snapdragon Laptop (PC Hub + LLM)](#2-snapdragon-laptop-pc-hub--llm)
  - [3. Arduino UNO Q (Voice Pipeline)](#3-arduino-uno-q-voice-pipeline)
  - [4. Phone Companion Dashboard](#4-phone-companion-dashboard)
- [Running the Full Demo](#running-the-full-demo)
- [Known Limitations](#known-limitations)
- [Team](#team)

---

## Overview

QUANTUMSHIFT-XR is a mixed reality shooter where the player's real room becomes the battlefield.
Aliens spawn around real furniture (via Quest 3 passthrough + Scene API), the player collects
hidden gems that raise the tension and difficulty as they go, and a boss character holds a real
spoken conversation with the player — powered by a locally-hosted LLM running on Snapdragon
hardware, not a cloud API.

<!--
SCREENSHOT 2: 15–30 second overview GIF (optional but strongly recommended)
Type: Screen-recorded gameplay clip converted to GIF, showing: player looks around real room →
shoots an alien → picks up a gem → talks to the boss. This is the single highest-impact visual
in the whole README. Tools: ScreenToGif (Windows, free) or ezgif.com to convert an MP4 to GIF.
Keep it under ~10MB so it loads on GitHub without lag.
-->

## Features

- 🔫 **Passthrough MR shooting** — real room, raycast-based combat, physics-layer filtered hit detection
- 💎 **Gem hunting objective** — 5 hidden gems trigger difficulty ramp + field-of-view darkening
- 🗣️ **Voice-to-voice boss fight** — multilingual speech in, LLM reasoning, spoken response out
- 🧠 **On-device LLM** — Qwen 7B running locally via Ollama on a Snapdragon X Elite laptop, zero cloud LLM dependency
- 🎙️ **Offline keyword spotting** — pause/resume voice commands via Arduino UNO Q, no network round-trip
- 📱 **Companion dashboard** — second player controls difficulty live from a phone browser, no app install

## System Architecture

<!--
SCREENSHOT 3: Architecture diagram
Type: A clean block diagram showing your 4 devices (Quest 3, Snapdragon laptop, UNO Q, phone)
and the arrows between them with protocol labels (WebSocket, REST, etc.) — similar style to the
"USB Microphone → keyword_spotting → board" diagram in your uploaded App Lab screenshots.
You can build this quickly in draw.io, Excalidraw, or even PowerPoint/Google Slides, then export
as PNG. This single image does more to convince judges you have a real distributed architecture
than several paragraphs of text — prioritize this one if you're short on time.
-->

![System Architecture](docs/screenshots/architecture.png)

| Device | Role | Key Tech |
|---|---|---|
| Meta Quest 3 | Rendering, passthrough, mic/speaker I/O | Unity 6, AR Foundation (Plane/Bounding Box Manager) |
| Snapdragon X Elite laptop | PC Hub, LLM inference, STT/TTS relay | Python, Ollama (Qwen 7B), Sarvam AI (Saaras STT / Bulbul TTS) |
| Arduino UNO Q | Offline keyword spotting (pause/resume) | Vosk, WebSocket, Arduino App Lab |
| Snapdragon phone | Companion dashboard, live difficulty control | Browser-based, no native app |

## Hardware & Software Requirements

**Hardware**
- Meta Quest 3
- A Windows laptop with a Snapdragon X Elite processor
- Arduino UNO Q
- A phone with a modern browser (Snapdragon-powered, per hackathon kit)
- Wi-Fi router/hotspot (all devices must be on the same network)

**Software**
- Unity 6 (with Android/Quest build support installed)
- Arduino App Lab
- Python 3.10+
- Ollama
- A Sarvam AI API key ([get one here](https://www.sarvam.ai))

## Project Structure

```
quantumshift-xr/
├── unity-project/           # Full Unity project — open this in Unity Hub
│   ├── Assets/
│   └── ...
├── pc-hub/                  # Python services running on the Snapdragon laptop
│   ├── orchestrator.py
│   ├── cloud_client.py      # LLM (Ollama) + Sarvam STT/TTS calls
│   └── requirements.txt
├── uno-q-app/                # Arduino App Lab project (keyword spotting)
│   ├── python/
│   │   └── server.py
│   └── vosk-model-small-en-us-0.15/   # not committed — see setup below
├── phone-dashboard/          # Static web dashboard
│   └── index.html
├── docs/
│   └── screenshots/          # All README images live here
└── README.md
```

<!--
SCREENSHOT 4: Repo file tree
Type: A screenshot of your actual GitHub repo's file/folder view once everything is pushed,
OR a screenshot of your local folder structure in VS Code's sidebar. Place this right after the
project structure code block above, so judges can visually confirm the repo matches the
described layout.
-->

## Setup Guide

> ⚠️ Do these in order — later steps assume earlier ones are running.

### 1. Unity + Meta Quest 3

1. Open `unity-project/` in Unity Hub (Unity 6).
2. Connect your Quest 3 via Link cable or Air Link for testing, or build a standalone APK for the final demo.
3. In `Assets/Scripts/NetworkConfig`, set `PC_HUB_IP` to your laptop's local IP address (see Step 2).
4. Press Play (tethered) or install the built APK (standalone).

<!--
SCREENSHOT 5: Unity Editor
Type: Screenshot of the Unity Editor with the main game scene open in the Hierarchy/Scene view,
showing your key GameObjects (player rig, spawner, boss, etc.) — demonstrates the project is
real and organized, not just a single script. Place at the end of this section.
-->

### 2. Snapdragon Laptop (PC Hub + LLM)

```bash
cd pc-hub
pip install -r requirements.txt
```

Install and start Ollama, then pull the model:
```bash
ollama pull qwen:7b
```

Set your Sarvam API key as an environment variable:
```bash
setx SARVAM_API_KEY "your_key_here"    # Windows
```

Run the hub:
```bash
python orchestrator.py
```
You should see `Listening on ws://0.0.0.0:<port>` — note this machine's IP address (`ipconfig` on Windows), you'll need it for the Quest and UNO Q.

<!--
SCREENSHOT 6: PC Hub terminal running
Type: Terminal window showing orchestrator.py running cleanly with the "Listening on..." message
visible — same style as the terminal output you've been pasting into this chat. Proves the
backend actually starts without errors.
-->

### 3. Arduino UNO Q (Voice Pipeline)

1. Open `uno-q-app/` in Arduino App Lab.
2. Connect the UNO Q to the same Wi-Fi network as your laptop.
3. Download the Vosk model (one-time):
   ```bash
   wget https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip
   unzip vosk-model-small-en-us-0.15.zip -d uno-q-app/python/
   ```
4. In App Lab, set `PC_HUB_IP` inside `python/server.py` to your laptop's IP from Step 2.
5. Click **Run** in App Lab.

<!--
SCREENSHOT 7: App Lab running the keyword spotting app
Type: Screenshot of the Arduino App Lab interface (like the ones you uploaded) showing your
project running, ideally with the console panel open showing "Listening on ws://0.0.0.0:8765"
or a keyword detection event live. Place at the end of this section.
-->

### 4. Phone Companion Dashboard

1. Ensure your phone is on the same Wi-Fi network.
2. Open a browser and navigate to `http://<PC_HUB_IP>:<dashboard_port>`.
3. You should see the live commentary feed and the wave-trigger button.

<!--
SCREENSHOT 8: Phone dashboard
Type: Screenshot of the actual dashboard open in a phone browser, ideally with a real commentary
line visible on screen (not a blank/empty state). Place at the end of this section.
-->

## Running the Full Demo

1. Start the **PC Hub** first (Step 2) — everything else connects to it.
2. Start the **UNO Q** app (Step 3).
3. Launch the **Unity build** on the Quest 3 (Step 1).
4. Open the **phone dashboard** (Step 4).
5. Put on the headset, walk around your scanned room, and start playing.

<!--
SCREENSHOT 9 (optional but recommended): Full demo photo
Type: A wide photo of your actual physical setup mid-demo — laptop, UNO Q, phone, and someone
wearing the Quest, all visible together. This is the "proof it's real, not just slides" shot
judges respond well to. Place at the very end of the README, above Team.
-->

## Known Limitations

- The boss conversation pipeline (Sarvam STT → local LLM → Sarvam TTS) requires internet access for the STT/TTS calls; the LLM itself runs fully offline.
- Environment segmentation for furniture-as-cover is a stretch feature and may fall back to fixed spawn points if Quest Scene API data is limited.
- Keyword spotting for pause/resume is tuned for a quiet-to-moderate noise environment; very loud rooms may require repeating the command.

## Team

| Name | Role |
|---|---|
| _Name_ | _Role_ |
| _Name_ | _Role_ |
| _Name_ | _Role_ |

---

*Built in 24 hours at the Snapdragon Multiverse Hackathon Finale.*
