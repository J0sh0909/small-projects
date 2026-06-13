import json
import time
import random
import threading
import sys
import os

try:
    import pyautogui
    import keyboard
    import psutil
except ImportError:
    sys.exit(1)

pyautogui.FAILSAFE = True
pyautogui.PAUSE = 0

BASE_DIR    = os.path.dirname(sys.executable if getattr(sys, "frozen", False) else os.path.abspath(__file__))
CONFIG_PATH = os.path.join(BASE_DIR, "config.json")
LOG_PATH    = os.path.join(BASE_DIR, "click_log.txt")
PID_PATH    = os.path.join(BASE_DIR, "autoclicker.pid")


def kill_existing():
    if not os.path.exists(PID_PATH):
        return False

    with open(PID_PATH, "r") as f:
        pid = int(f.read().strip())

    try:
        p = psutil.Process(pid)
        p.kill()
    except psutil.NoSuchProcess:
        pass

    os.remove(PID_PATH)
    return True


def write_pid():
    with open(PID_PATH, "w") as f:
        f.write(str(os.getpid()))


def cleanup_pid():
    if os.path.exists(PID_PATH):
        os.remove(PID_PATH)


def load_config():
    with open(CONFIG_PATH, "r") as f:
        cfg = json.load(f)

    cps_val = cfg.get("clicks_per_second", 0) or 0
    cpm_val = cfg.get("clicks_per_minute", 0) or 0
    cph_val = cfg.get("clicks_per_hour",   0) or 0

    active = [(v, k) for v, k in [
        (cps_val, "clicks_per_second"),
        (cpm_val, "clicks_per_minute"),
        (cph_val, "clicks_per_hour")
    ] if v > 0]

    if len(active) == 0:
        raise ValueError("All rate fields are 0.")
    if len(active) > 1:
        fields = ", ".join(k for _, k in active)
        raise ValueError(f"More than one rate field is set: {fields}.")

    rate_value, rate_type = active[0]

    if rate_type == "clicks_per_second":
        cps = rate_value
    elif rate_type == "clicks_per_minute":
        cps = rate_value / 60.0
    elif rate_type == "clicks_per_hour":
        cps = rate_value / 3600.0

    if cps <= 0:
        raise ValueError("Calculated CPS must be greater than 0.")

    return {
        "x":             cfg.get("x"),
        "y":             cfg.get("y"),
        "cps":           cps,
        "base_interval": 1.0 / cps,
        "button":        cfg.get("button", "left"),
        "stop_key":      cfg.get("stop_key", "F6")
    }


def random_interval(base):
    strategy = random.random()

    if strategy < 0.33:
        return random.uniform(base * 0.4, base * 1.6)
    elif strategy < 0.66:
        return max(base * 0.15, random.gauss(base, base * 0.25))
    else:
        return max(base * 0.15, random.betavariate(2, 2) * base * 2)


def click_loop(config, stop_event):
    x      = config["x"]
    y      = config["y"]
    button = config["button"]
    base   = config["base_interval"]
    count  = 0

    with open(LOG_PATH, "w") as log:
        log.write("--- Session started ---\n")

        while not stop_event.is_set():
            if x is not None and y is not None:
                pyautogui.click(x=x, y=y, button=button)
            else:
                pyautogui.click(button=button)

            interval = random_interval(base)
            count += 1
            log.write(f"Click #{count:<6} —   {interval * 1000:.2f}ms\n")
            log.flush()

            deadline = time.perf_counter() + interval
            while time.perf_counter() < deadline:
                if stop_event.is_set():
                    break
                time.sleep(0.005)

        log.write(f"--- Session ended — {count} total clicks ---\n")


def main():
    # Second press — kill the running instance and exit
    if kill_existing():
        sys.exit(0)

    # First press — register this instance and start clicking
    try:
        config = load_config()
    except Exception:
        sys.exit(1)

    write_pid()

    stop_event   = threading.Event()
    click_thread = threading.Thread(target=click_loop, args=(config, stop_event), daemon=True)
    click_thread.start()

    def force_stop(e=None):
        stop_event.set()
        cleanup_pid()
        sys.exit(0)

    keyboard.add_hotkey(config["stop_key"], force_stop)

    try:
        click_thread.join()
    except KeyboardInterrupt:
        stop_event.set()
    finally:
        cleanup_pid()


if __name__ == "__main__":
    main()
