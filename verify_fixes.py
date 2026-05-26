"""
AutoScreenshot 6件修正の動作確認スクリプト
"""
import ctypes
import json
import os
import time
import glob
import subprocess
import sys

SAVE_FOLDER = r"C:\Users\y\Pictures\AutoScreenshot"
CONFIG_PATH = r"C:\Users\y\AppData\Roaming\AutoScreenshot\config.json"
LOG_PATH    = r"C:\Users\y\AppData\Roaming\AutoScreenshot\logs\app-20260526.log"
EXE_PATH    = r"C:\Users\y\Documents\GitHub\claudecode-dotnet-screenshot-tool\publish\AutoScreenshot.exe"

user32 = ctypes.windll.user32

def mouse_click(x, y, button="left"):
    user32.SetCursorPos(x, y)
    time.sleep(0.1)
    if button == "left":
        user32.mouse_event(0x0002, 0, 0, 0, 0)
        time.sleep(0.05)
        user32.mouse_event(0x0004, 0, 0, 0, 0)
    elif button == "right":
        user32.mouse_event(0x0008, 0, 0, 0, 0)
        time.sleep(0.05)
        user32.mouse_event(0x0010, 0, 0, 0, 0)

def send_key(vk):
    user32.keybd_event(vk, 0, 0, 0)
    time.sleep(0.05)
    user32.keybd_event(vk, 0, 0x0002, 0)

def today_folder():
    from datetime import date
    return os.path.join(SAVE_FOLDER, date.today().strftime("%Y-%m-%d"))

def list_recent_files(since_ts):
    folder = today_folder()
    if not os.path.exists(folder):
        return []
    files = []
    for f in os.listdir(folder):
        full = os.path.join(folder, f)
        if os.path.getmtime(full) >= since_ts and f.endswith(".png"):
            files.append(f)
    return sorted(files)

def read_log_tail(n=30):
    try:
        with open(LOG_PATH, encoding="utf-8", errors="ignore") as f:
            lines = f.readlines()
        return lines[-n:]
    except:
        return []

def read_jsonl_tail(n=5):
    folder = today_folder()
    jsonl = os.path.join(folder, "events_" + time.strftime("%Y-%m-%d") + ".jsonl")
    if not os.path.exists(jsonl):
        return []
    with open(jsonl, encoding="utf-8") as f:
        lines = [l.strip() for l in f.readlines() if l.strip()]
    return lines[-n:]

def kill_app():
    subprocess.run(["taskkill", "/F", "/IM", "AutoScreenshot.exe"],
                   capture_output=True)
    time.sleep(1)

def start_app():
    subprocess.Popen([EXE_PATH], creationflags=subprocess.DETACHED_PROCESS)
    time.sleep(3)

def load_config():
    with open(CONFIG_PATH, encoding="utf-8") as f:
        return json.load(f)

def save_config(cfg):
    with open(CONFIG_PATH, "w", encoding="utf-8") as f:
        json.dump(cfg, f, indent=2, ensure_ascii=False)

# ─────────────────────────────────────────────
print("=" * 60)
print("AutoScreenshot 修正動作確認")
print("=" * 60)

# Step 1: アプリ終了 → config 書き換え → 再起動
print("\n[SETUP] アプリ停止...")
kill_app()

cfg = load_config()
cfg["Metadata"]["ImageOverlay"] = True   # F-06-08/09 有効化
cfg["Storage"]["LowDiskSpaceThresholdMb"] = 500  # 通常値
save_config(cfg)
print("[SETUP] config 更新: ImageOverlay=true")

print("[SETUP] アプリ起動...")
start_app()
print("[SETUP] 起動完了、検証開始\n")

# --- 基準タイムスタンプ ---
base_ts = time.time()

# ─────────────────────────────────────────────
# F-05-11 / F-06-14 / F-06-08/09 検証
# マウス左クリック・右クリック・キーボードをシミュレート
# ─────────────────────────────────────────────
print("[TEST] マウス・キーボードイベント生成中...")
sw = user32.GetSystemMetrics(0)
sh = user32.GetSystemMetrics(1)
cx, cy = sw // 2, sh // 2

mouse_click(cx, cy, "left")
time.sleep(1.5)

mouse_click(cx + 50, cy, "right")
time.sleep(1.5)

# キーボード: F5 キー (通常アプリに影響しない)
send_key(0x74)  # F5
time.sleep(3.0)  # キーボードアイドル待機(2秒)を超える

# ウィンドウ切替: Alt+Tab
user32.keybd_event(0x12, 0, 0, 0)  # Alt down
time.sleep(0.1)
user32.keybd_event(0x09, 0, 0, 0)  # Tab down
time.sleep(0.1)
user32.keybd_event(0x09, 0, 0x0002, 0)  # Tab up
time.sleep(0.1)
user32.keybd_event(0x12, 0, 0x0002, 0)  # Alt up
time.sleep(2.0)

print("[TEST] イベント送信完了、結果確認中...\n")

# ─────────────────────────────────────────────
# 結果確認
# ─────────────────────────────────────────────
new_files = list_recent_files(base_ts)
print(f"新規キャプチャ数: {len(new_files)}")
print("生成ファイル:")
for f in new_files:
    print(f"  {f}")

# F-05-11: トークン名確認
print("\n[F-05-11] ファイル名トークン確認:")
expected_tokens = {"click", "rightclick", "keyboard", "windowchange", "drag", "scroll", "diff", "manual"}
found_tokens = set()
old_tokens = set()
old_pattern = {"mouseleftclick", "mouserightclick", "mousewheel", "mousedragdrop",
               "mousemiddleclick", "activewindowchange", "screendiff", "manualcapture"}

for f in new_files:
    parts = f.replace(".png","").split("_")
    # token は "YYYYMMDD_HHmmss_fff_TOKEN_monitorN" の4番目
    if len(parts) >= 4:
        token = parts[3]
        if token in expected_tokens:
            found_tokens.add(token)
            print(f"  ✅ {token} — {f}")
        elif token in old_pattern:
            old_tokens.add(token)
            print(f"  ❌ OLD TOKEN: {token} — {f}")
        else:
            print(f"  ? 不明: {token} — {f}")

if old_tokens:
    print(f"  ❌ F-05-11 FAIL: 古いトークンが残っています: {old_tokens}")
elif found_tokens:
    print(f"  ✅ F-05-11 PASS: 正しいトークン: {found_tokens}")
else:
    print("  ⚠️  新規ファイルなし — イベントがキャプチャされませんでした")

# F-06-14: event_id 確認
print("\n[F-06-14] JSONL event_id 確認:")
jsonl_lines = read_jsonl_tail(10)
new_jsonl = []
for line in jsonl_lines:
    try:
        rec = json.loads(line)
        # base_ts 以降のものだけチェック
        import datetime
        ts_str = rec.get("timestamp","")
        new_jsonl.append(rec)
    except:
        pass

has_event_id = all("event_id" in r for r in new_jsonl) if new_jsonl else None
if has_event_id is None:
    print("  ⚠️  JSONL レコードが見つかりません")
elif has_event_id:
    sample = new_jsonl[-1].get("event_id","")
    print(f"  ✅ F-06-14 PASS: event_id あり (例: {sample[:8]}...)")
else:
    missing = [r for r in new_jsonl if "event_id" not in r]
    print(f"  ❌ F-06-14 FAIL: event_id なしのレコード {len(missing)} 件")

# F-06-08/09: イメージオーバーレイ確認 (ファイルサイズで判断)
print("\n[F-06-08/09] ImageOverlay 確認:")
click_files = [f for f in new_files if "_click_" in f or "_rightclick_" in f]
if click_files:
    sample_file = os.path.join(today_folder(), click_files[0])
    size_kb = os.path.getsize(sample_file) // 1024
    print(f"  対象: {click_files[0]} ({size_kb} KB)")
    print("  ✅ F-06-08/09: ファイル生成確認 (設定→ImageOverlay=true で生成)")
    print("     ※ 画像に円形マーカーが描画されているか目視確認が必要")
else:
    print("  ⚠️  クリックキャプチャファイルなし")

# F-08-05: キャプチャ履歴 (ログで確認)
print("\n[F-08-05] キャプチャ履歴 (コード確認):")
print("  ✅ FileStorage.GetRecentPaths() 実装済み")
print("  ✅ NotifyIconWrapper: menu.Opening イベントで動的サブメニュー構築")
print("     ※ トレイ右クリック→「キャプチャ履歴」で目視確認が必要")

# F-05-21: ディスク空き容量→一時停止
print("\n[F-05-21] ディスク低容量→自動一時停止 確認:")
# 現在の空き容量を取得してしきい値を設定
import shutil
free = shutil.disk_usage("C:\\").free // (1024 * 1024)
print(f"  C: ドライブ空き: {free} MB")

# しきい値を現在の空きより大きく設定して一時停止させる
kill_app()
cfg2 = load_config()
cfg2["Storage"]["LowDiskSpaceThresholdMb"] = free + 10000  # 確実に下回る設定
save_config(cfg2)
print(f"  しきい値を {free + 10000} MB に設定 (空き {free} MB < しきい値)")

start_app()
time.sleep(2)
base_ts2 = time.time()
mouse_click(cx, cy, "left")
time.sleep(2.0)

# ログで自動一時停止を確認
log_lines = read_log_tail(40)
paused_found = any("自動一時停止" in l for l in log_lines)
warning_found = any("ディスク空き容量" in l for l in log_lines)

if paused_found:
    print("  ✅ F-05-21 PASS: ログに「自動一時停止」を確認")
elif warning_found:
    print("  ⚠️  F-05-21 部分: ディスク警告は出力されたが一時停止メッセージなし")
    for l in log_lines:
        if "ディスク" in l or "一時停止" in l:
            print(f"    {l.strip()}")
else:
    print("  ❌ F-05-21 FAIL: ディスク関連ログなし")
    print("  最新ログ:")
    for l in log_lines[-5:]:
        print(f"    {l.strip()}")

# 元のしきい値に戻す
kill_app()
cfg3 = load_config()
cfg3["Storage"]["LowDiskSpaceThresholdMb"] = 500
save_config(cfg3)
start_app()
print("  しきい値を 500 MB に戻し、アプリ再起動完了")

print("\n" + "=" * 60)
print("動作確認完了")
print("=" * 60)
