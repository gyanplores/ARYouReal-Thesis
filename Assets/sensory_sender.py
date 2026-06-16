import socket
import time

# ─── CONFIGURATION ────────────────────────────────────────────
UNITY_IP = "192.168.1.xxx"  # <-- CHANGE to Unity device's IP
UNITY_PORT = 5006
# ──────────────────────────────────────────────────────────────

MODES = {
    "1": "V+A+H  (See + Hear + Feel)",
    "2": "V+A    (See + Hear)",
    "3": "V+H    (See + Feel)",
    "4": "A+H    (Hear + Feel)",
    "5": "V only (See only)",
    "6": "A only (Hear only)",
    "7": "H only (Feel only)",
}

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

print("=" * 40)
print("  Sensory Mode Sender")
print(f"  Sending to {UNITY_IP}:{UNITY_PORT}")
print("=" * 40)
print()
print("Available modes:")
for key, value in MODES.items():
    print(f"  {key} = {value}")
print()
print("Type a number (1-7) and press Enter.")
print("Type 'q' to quit.")
print()

while True:
    try:
        user_input = input("Enter mode: ").strip()

        if user_input.lower() == 'q':
            print("Exiting.")
            break

        if user_input in MODES:
            sock.sendto(user_input.encode(), (UNITY_IP, UNITY_PORT))
            print(f"  → Sent mode {user_input}: {MODES[user_input]}")
        else:
            print("  ! Invalid input. Please enter 1-7 or 'q' to quit.")

    except KeyboardInterrupt:
        print("\nStopped.")
        break

sock.close()
