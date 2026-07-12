#!/usr/bin/env python3
"""
Tactical Hub — WebSocket Relay Server for Game Master Spawning
==============================================================
A lightweight relay that receives JSON commands from the phone UI
and broadcasts them to all connected Unity clients.

Usage:
    python tactical_hub.py

Listens on 0.0.0.0:8765
"""

import asyncio
import json
import signal
import sys
from datetime import datetime

try:
    import websockets
except ImportError:
    print("ERROR: 'websockets' library not found.")
    print("Install it with:  pip install websockets")
    sys.exit(1)

# ── Configuration ──────────────────────────────────────────────
HOST = "0.0.0.0"
PORT = 8765

# ── State ──────────────────────────────────────────────────────
connected_clients = set()
message_count = 0

# ── Helpers ────────────────────────────────────────────────────
def timestamp():
    return datetime.now().strftime("%H:%M:%S")

def print_banner():
    print()
    print("=" * 60)
    print("  TACTICAL HUB — Game Master WebSocket Relay")
    print("=" * 60)
    print(f"  Listening on  ws://{HOST}:{PORT}")
    print(f"  Started at    {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print()
    print("  Waiting for connections from Phone UI and Unity...")
    print("=" * 60)
    print()

# ── WebSocket Handler ─────────────────────────────────────────
async def handler(websocket):
    """Handle a single WebSocket connection."""
    global message_count

    remote = websocket.remote_address
    client_label = f"{remote[0]}:{remote[1]}"
    connected_clients.add(websocket)

    print(f"[{timestamp()}] ✅ CONNECTED: {client_label}  "
          f"(total clients: {len(connected_clients)})")

    try:
        async for raw_message in websocket:
            message_count += 1

            # Try to pretty-print JSON, fall back to raw string
            try:
                data = json.loads(raw_message)
                device = data.get("device", "unknown")
                action = data.get("action", "unknown")
                enemy_type = data.get("type", "unknown")
                x = data.get("x", 0)
                y = data.get("y", 0)
                scale = data.get("scale", 1.0)

                print(f"[{timestamp()}] 📡 MSG #{message_count} from {client_label}")
                print(f"           device={device}  action={action}  "
                      f"type={enemy_type}")
                print(f"           x={x:.1f}  y={y:.1f}  scale={scale:.1f}")
            except (json.JSONDecodeError, ValueError):
                print(f"[{timestamp()}] 📡 MSG #{message_count} from {client_label}")
                print(f"           (raw) {raw_message[:200]}")

            # Broadcast to ALL other connected clients
            targets = connected_clients - {websocket}
            if targets:
                broadcast_tasks = [
                    client.send(raw_message) for client in targets
                ]
                results = await asyncio.gather(
                    *broadcast_tasks, return_exceptions=True
                )
                success = sum(1 for r in results if not isinstance(r, Exception))
                print(f"           → Relayed to {success}/{len(targets)} client(s)")
            else:
                print(f"           → No other clients to relay to")

    except websockets.exceptions.ConnectionClosed as e:
        print(f"[{timestamp()}] 🔌 DISCONNECTED: {client_label}  "
              f"(code={e.code}, reason={e.reason or 'none'})")
    except Exception as e:
        print(f"[{timestamp()}] ❌ ERROR with {client_label}: {e}")
    finally:
        connected_clients.discard(websocket)
        print(f"[{timestamp()}]    Remaining clients: {len(connected_clients)}")

# ── Main ───────────────────────────────────────────────────────
async def main():
    print_banner()

    # Graceful shutdown
    stop = asyncio.Future()

    def signal_handler():
        if not stop.done():
            stop.set_result(True)

    loop = asyncio.get_running_loop()
    for sig in (signal.SIGINT, signal.SIGTERM):
        try:
            loop.add_signal_handler(sig, signal_handler)
        except NotImplementedError:
            # Windows doesn't support add_signal_handler for SIGTERM
            pass

    async with websockets.serve(handler, HOST, PORT):
        print(f"[{timestamp()}] 🟢 Server is LIVE on ws://{HOST}:{PORT}")
        print()

        try:
            await stop
        except asyncio.CancelledError:
            pass

    print(f"\n[{timestamp()}] 🔴 Server shut down. "
          f"Total messages relayed: {message_count}")

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print(f"\n[{timestamp()}] 🔴 Interrupted. Goodbye!")
