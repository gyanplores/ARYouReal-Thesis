import time
import socket
import json
import random

import cflib.crtp

from cflib.crazyflie import Crazyflie
from cflib.crazyflie.syncCrazyflie import SyncCrazyflie
from cflib.crazyflie.log import LogConfig


# ==================================================
# VALID AFFORDANCE COMBINATIONS
# ==================================================

VALID_COMBOS = {
    "VAH", "VA", "AH", "VH", "V", "A", "H"
}


# ==================================================
# STEP 1: ALL USER INPUTS BEFORE DRONE CONNECTS
# (IP, eye level, mode, affordance sequence)
# ==================================================

print("=" * 50)
print("  AR You Real — Trial Setup")
print("=" * 50)
print()

UNITY_IP = input("Enter Quest / Unity IP: ").strip()

USER_Z = float(input("Enter subject eye level vertical height in meters: "))

print()
print("Enter affordance combinations one at a time.")
print("Valid options: VAH, VA, AH, VH, V, A, H")
print("Type 'done' when finished.\n")

combos = []
while True:
    entry = input(f"Combination #{len(combos) + 1} (or 'done'): ").strip().upper()

    if entry == "DONE":
        if len(combos) == 0:
            print("  ! You need at least one combination before finishing.")
            continue
        break

    if entry not in VALID_COMBOS:
        print(f"  ! '{entry}' is not valid. Choose from: {', '.join(sorted(VALID_COMBOS))}")
        continue

    combos.append(entry)
    print(f"  Added: {entry}")

print(f"\n  {len(combos)} combination(s) entered.")
print()
input("Press Enter to connect to the drone and begin...")


# ==================================================
# NETWORK
# ==================================================

UDP_PORT = 5005

udp_send = socket.socket(
    socket.AF_INET,
    socket.SOCK_DGRAM
)


# ==================================================
# DRONE SETTINGS
# ==================================================

URI = "radio://0/80/2M"

USER_X = 0.0
USER_Y = -2.0

MIN_DISTANCE = 1.5


# ==================================================
# GLOBAL STATE
# ==================================================

running = True

current_x = 0.0
current_y = 0.0
current_z = 0.0

commander = None


# ==================================================
# UDP SEND TO UNITY
# ==================================================

def send_udp(packet):
    try:
        udp_send.sendto(
            json.dumps(packet).encode(),
            (UNITY_IP, UDP_PORT)
        )
    except Exception as e:
        print("UDP SEND ERROR", e)


# ==================================================
# POSITION CALLBACK (real drone telemetry)
# ==================================================

def log_position(ts, data, logconf):
    global current_x, current_y, current_z

    current_x = data["stateEstimate.x"]
    current_y = data["stateEstimate.y"]
    current_z = data["stateEstimate.z"]

    print(
        f"\rREAL {current_x:.2f} {current_y:.2f} {current_z:.2f}",
        end=""
    )


# ==================================================
# SAFETY — BOUNDS + LIGHTHOUSE BARRIERS + USER RADIUS
# ==================================================

def clamp_user_distance(x, y, z):

    # ==========================================
    # HARD WORLD BOUNDS
    # X: [-2, 2]   Y: [-2, 2]   Z: [0, 2]
    # X/Y are horizontal axes, Z is vertical altitude
    # ==========================================

    x = max(-2.0, min(2.0, x))
    y = max(-2.0, min(2.0, y))
    z = max(0.0, min(2.0, z))

    # ==========================================
    # USER SAFETY RADIUS
    # user at (0, -2)
    # ==========================================

    dx = x - USER_X
    dy = y - USER_Y
    d = (dx * dx + dy * dy) ** 0.5

    if d < MIN_DISTANCE:
        if d < 0.001:
            dx = 0
            dy = 1
            d = 1

        scale = MIN_DISTANCE / d
        x = USER_X + dx * scale
        y = USER_Y + dy * scale

        print("\n[SAFETY] User radius correction")

    # ==========================================
    # LIGHTHOUSE BARRIER
    #
    # top-left:     x:[-2,-1.5]  y:[1.5,2]
    # bottom-right: x:[1.5,2]    y:[-2,-1.5]
    # ==========================================

    INSIDE_TOP_LEFT = (-2.0 <= x <= -1.5) and (1.5 <= y <= 2.0)
    INSIDE_BOTTOM_RIGHT = (1.5 <= x <= 2.0) and (-2.0 <= y <= -1.5)

    if INSIDE_TOP_LEFT:
        x = -1.49
        y = 1.49
        print("\n[SAFETY] Lighthouse 1 barrier")

    if INSIDE_BOTTOM_RIGHT:
        x = 1.49
        y = -1.49
        print("\n[SAFETY] Lighthouse 2 barrier")

    # Final clamp
    x = max(-2.0, min(2.0, x))
    y = max(-2.0, min(2.0, y))
    z = max(0.0, min(2.0, z))

    return (x, y, z)


# ==================================================
# RANDOM COORDINATE GENERATOR
# ==================================================

def generate_safe_coordinate():
    x = random.uniform(-2.0, 2.0)
    y = random.uniform(-2.0, 2.0)
    z = random.uniform(0.0, 2.0)
    return clamp_user_distance(x, y, z)


# ==================================================
# MOVE REAL DRONE + NOTIFY UNITY
# ==================================================

def move_drone(x, y, z, affordance, hold=3):

    x, y, z = clamp_user_distance(x, y, z)

    commander.go_to(x, y, z, 0, 2)

    # Tell Unity the real drone position and the affordance.
    # Unity itself decides MASK vs MIRROR via its own startup menu.
    send_udp({
        "type": "TRIAL",
        "affordance": affordance,
        "x": x,
        "y": y,
        "z": z
    })

    print(f"\n[MOVE] affordance={affordance} -> ({x:.2f}, {y:.2f}, {z:.2f})")

    time.sleep(hold)


# ==================================================
# BUILD TRIAL LIST
# ==================================================

def build_trials(combo_list):
    trials = []
    for combo in combo_list:
        x, y, z = generate_safe_coordinate()
        trials.append({
            "affordance": combo,
            "x": x,
            "y": y,
            "z": z
        })
    return trials


# ==================================================
# RUN TRIAL SEQUENCE
# ==================================================

def run_trials(trials):
    print("\n" + "=" * 50)
    print("  TRIAL SEQUENCE")
    print("=" * 50)
    for i, trial in enumerate(trials, 1):
        print(f"  {i}. {trial['affordance']:<4} -> "
              f"({trial['x']:.2f}, {trial['y']:.2f}, {trial['z']:.2f})")
    print()

    for i, trial in enumerate(trials, 1):
        input(f"\nPress Enter to move to trial {i}/{len(trials)} "
              f"[{trial['affordance']}]...")

        move_drone(
            trial["x"],
            trial["y"],
            trial["z"],
            trial["affordance"]
        )

        print(f"[TRIAL {i}/{len(trials)}] {trial['affordance']} "
              f"active at ({trial['x']:.2f}, {trial['y']:.2f}, {trial['z']:.2f})")

    input("\nPress Enter to land the drone and end the session...")


# ==================================================
# MAIN — DRONE CONNECTION + FLIGHT
# ==================================================

cflib.crtp.init_drivers()

with SyncCrazyflie(
    URI,
    cf=Crazyflie(rw_cache="./cache")
) as scf:

    cf = scf.cf

    print("Connected to Crazyflie")

    cf.param.set_value("stabilizer.estimator", "2")
    cf.param.set_value("stabilizer.controller", "2")
    cf.param.set_value("commander.enHighLevel", "1")
    cf.param.set_value("kalman.resetEstimation", "1")

    time.sleep(0.2)

    cf.param.set_value("kalman.resetEstimation", "0")

    time.sleep(5)

    cf.supervisor.send_arming_request(True)

    time.sleep(2)

    commander = cf.high_level_commander

    logconf = LogConfig("pos", 100)
    logconf.add_variable("stateEstimate.x", "float")
    logconf.add_variable("stateEstimate.y", "float")
    logconf.add_variable("stateEstimate.z", "float")

    cf.log.add_config(logconf)
    logconf.data_received_cb.add_callback(log_position)
    logconf.start()

    commander.takeoff(1.5, 3)
    time.sleep(4)

    try:
        trials = build_trials(combos)
        run_trials(trials)

    except KeyboardInterrupt:
        print("\n[EMERGENCY] Stopping early.")

    finally:
        commander.land(0, 3)
        time.sleep(4)
        commander.stop()
        logconf.stop()
        print("\nDone")
