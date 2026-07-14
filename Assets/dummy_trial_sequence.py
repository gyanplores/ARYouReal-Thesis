import time
import socket
import json
import random
import threading


# ==================================================
# VALID AFFORDANCE COMBINATIONS
# ==================================================

VALID_COMBOS = {"VAH", "VA", "AH", "VH", "V", "A", "H"}


# ==================================================
# STEP 1: USER INPUTS
# ==================================================

print("=" * 50)
print("  AR You Real — Trial Setup")
print("=" * 50)
print()

UNITY_IP = input("Enter Quest / Unity IP: ").strip()
USER_Z = float(input("Enter subject eye level height in meters: "))

print()
print("Enter affordance combinations one at a time.")
print("Valid: VAH, VA, AH, VH, V, A, H")
print("Type 'done' when finished.\n")

combos = []
while True:
    entry = input(f"Combination #{len(combos) + 1} (or 'done'): ").strip().upper()
    if entry == "DONE":
        if len(combos) == 0:
            print("  ! Need at least one combination.")
            continue
        break
    if entry not in VALID_COMBOS:
        print(f"  ! Invalid. Choose from: {', '.join(sorted(VALID_COMBOS))}")
        continue
    combos.append(entry)
    print(f"  Added: {entry}")

print(f"\n  {len(combos)} combination(s) entered.")
print()


# ==================================================
# STEP 2: TCP SERVER — wait for Unity READY signal
# ==================================================

TCP_PORT = 5007
UDP_PORT = 5005

tcp_server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
tcp_server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
tcp_server.bind(("0.0.0.0", TCP_PORT))
tcp_server.listen(1)

print(f"Waiting for Unity to connect (TCP port {TCP_PORT})...")
print("Start your Quest app now.\n")

tcp_conn, addr = tcp_server.accept()
print(f"Unity connected from {addr}")

# Wait for READY signal from Unity
buffer = ""
while True:
    data = tcp_conn.recv(4096).decode()
    buffer += data
    if "\n" in buffer:
        msg_raw, buffer = buffer.split("\n", 1)
        msg = json.loads(msg_raw)
        if msg.get("cmd") == "READY":
            print("Unity sent READY signal — proceeding.\n")
            break


# ==================================================
# STEP 3: NETWORK SETUP
# ==================================================

udp_send = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

# Global state
answers = []        # stores {trial, affordance, answer} per trial
current_answer = None
running = True


# ==================================================
# SAFETY BOUNDS
# ==================================================

USER_X = 0.0
USER_Y = -2.0
MIN_DISTANCE = 1.5

def clamp(x, y, z):
    x = max(-2.0, min(2.0, x))
    y = max(-2.0, min(2.0, y))
    z = max(0.0,  min(2.0, z))

    dx = x - USER_X
    dy = y - USER_Y
    d = (dx*dx + dy*dy) ** 0.5
    if d < MIN_DISTANCE:
        if d < 0.001: dx, dy, d = 0, 1, 1
        scale = MIN_DISTANCE / d
        x = USER_X + dx * scale
        y = USER_Y + dy * scale
        print("\n[SAFETY] User radius correction")

    if (-2.0 <= x <= -1.5) and (1.5 <= y <= 2.0):
        x, y = -1.49, 1.49
        print("\n[SAFETY] Lighthouse 1 barrier")
    if (1.5 <= x <= 2.0) and (-2.0 <= y <= -1.5):
        x, y = 1.49, -1.49
        print("\n[SAFETY] Lighthouse 2 barrier")

    return (max(-2.0, min(2.0, x)),
            max(-2.0, min(2.0, y)),
            max(0.0,  min(2.0, z)))

def generate_coordinate():
    return clamp(
        random.uniform(-2.0, 2.0),
        random.uniform(-2.0, 2.0),
        random.uniform(0.0,  2.0)
    )


# ==================================================
# UDP SEND TO UNITY
# ==================================================

def send_udp(packet):
    try:
        udp_send.sendto(json.dumps(packet).encode(), (UNITY_IP, UDP_PORT))
    except Exception as e:
        print(f"[UDP ERROR] {e}")


# ==================================================
# TCP LISTENER — receives ANSWER from Unity
# Runs in background thread
# ==================================================

def listen_unity():
    global current_answer, running
    buffer = ""
    while running:
        try:
            data = tcp_conn.recv(4096)
            if not data:
                break
            buffer += data.decode()
            while "\n" in buffer:
                msg_raw, buffer = buffer.split("\n", 1)
                msg = json.loads(msg_raw)
                print(f"\n[UNITY] Received: {msg}")
                if msg.get("cmd") == "ANSWER":
                    current_answer = {
                        "q1": msg.get("q1", 4),
                        "q2": msg.get("q2", 4),
                        "q3": msg.get("q3", 4),
                        "virtualGaze": round(msg.get("gazeTime", 0.0), 2),
                        "realGaze":    round(msg.get("realGazeTime", 0.0), 2)
                    }
                    print(f"[UNITY] Q1={current_answer['q1']} Q2={current_answer['q2']} Q3={current_answer['q3']} VGaze={current_answer['virtualGaze']}s RGaze={current_answer['realGaze']}s")
        except Exception as e:
            if running:
                print(f"[TCP ERROR] {e}")


# ==================================================
# DUMMY MOVE (simulates Crazyflie flight)
# ==================================================

def move_drone(x, y, z, affordance):
    x, y, z = clamp(x, y, z)
    print(f"\n[DRONE] Flying to ({x:.2f}, {y:.2f}, {z:.2f})...")
    time.sleep(2.0)  # simulate flight time
    print(f"[DRONE] Arrived. Sending TRIAL packet to Unity.")

    send_udp({
        "type": "TRIAL",
        "affordance": affordance,
        "x": x,
        "y": y,
        "z": z
    })

    print(f"[UDP] Sent → affordance={affordance} ({x:.2f}, {y:.2f}, {z:.2f})")


# ==================================================
# BUILD TRIAL LIST
# ==================================================

def get_drone_side(x):
    """Returns 'Right' if x > 0, 'Left' if x < 0, 'Center' if x == 0"""
    if x > 0:
        return "Right"
    elif x < 0:
        return "Left"
    else:
        return "Center"


def build_trials(combo_list):
    return [{"affordance": c, "x": x, "y": y, "z": z}
            for c in combo_list
            for x, y, z in [generate_coordinate()]]


# ==================================================
# RUN TRIAL SEQUENCE
# ==================================================

def run_trials(trials):
    global current_answer

    print("\n" + "=" * 50)
    print("  TRIAL SEQUENCE")
    print("=" * 50)
    for i, t in enumerate(trials, 1):
        print(f"  {i}. {t['affordance']:<4} → ({t['x']:.2f}, {t['y']:.2f}, {t['z']:.2f})")
    print()

    for i, trial in enumerate(trials, 1):
        input(f"\nPress Enter to start trial {i}/{len(trials)} [{trial['affordance']}]...")

        current_answer = None

        move_drone(trial["x"], trial["y"], trial["z"], trial["affordance"])

        print(f"[TRIAL {i}] Waiting for participant answers (3 sliders 1-7)...")
        while current_answer is None:
            time.sleep(0.1)

        print(f"[TRIAL {i}] Q1={current_answer['q1']} Q2={current_answer['q2']} Q3={current_answer['q3']} VGaze={current_answer['virtualGaze']}s RGaze={current_answer['realGaze']}s")

        # Determine which side each drone is on for labeling
        real_side    = get_drone_side(trial["x"])
        virtual_x    = -trial["x"]  # mirror flips X
        virtual_side = get_drone_side(virtual_x)

        answers.append({
            "trial": i,
            "affordance": trial["affordance"],
            "drone_side": real_side,
            "real_gaze_label":    f"{real_side} drone duration",
            "virtual_gaze_label": f"{virtual_side} drone duration",
            "real_gaze":    current_answer["realGaze"],
            "virtual_gaze": current_answer["virtualGaze"],
            "q1": current_answer["q1"],
            "q2": current_answer["q2"],
            "q3": current_answer["q3"]
        })

    print("\nAll trials complete.")


# ==================================================
# PRINT RESULTS TABLE
# ==================================================

def print_results():
    print("\n" + "=" * 100)
    print("  RESULTS")
    print("=" * 100)
    for r in answers:
        print(f"\n  Trial {r['trial']} — Affordance: {r['affordance']} | Real drone: {r['drone_side']}")
        print(f"    {r['real_gaze_label']}:    {r['real_gaze']}s")
        print(f"    {r['virtual_gaze_label']}: {r['virtual_gaze']}s")
        print(f"    Q1 (Which drone is real?):   {r['q1']}")
        print(f"    Q2 (Which drone is closer?): {r['q2']}")
        print(f"    Q3 (Which drone is safer?):  {r['q3']}")
    print()


# ==================================================
# MAIN
# ==================================================

# Start TCP listener thread
threading.Thread(target=listen_unity, daemon=True).start()

try:
    trials = build_trials(combos)

    # Send HOVER packet BEFORE trials so Unity sets baseline correctly
    # This ensures the first TRIAL packet produces a real non-zero delta
    send_udp({"type": "HOVER", "x": 0.0, "y": 0.0, "z": 1.5})
    print("[UDP] Sent HOVER baseline (0.0, 0.0, 1.5)")
    time.sleep(0.5)

    run_trials(trials)
    print_results()

except KeyboardInterrupt:
    print("\n[STOPPED] Session ended early.")
    if answers:
        print_results()

finally:
    running = False
    tcp_conn.close()
    tcp_server.close()
    udp_send.close()
    print("Done.")