#!/usr/bin/env bash
# Renders the widget gallery on an iOS simulator and copies the light/dark boards out (spec 04 §3.8, CLAUDE.md §5.3).
# Usage: render-gallery.sh <out-dir>
set -euo pipefail

out="${1:?usage: render-gallery.sh <out-dir>}"
dir="$(cd "$(dirname "$0")/.." && pwd)"
bundle_id="org.burdock.app.widgetgallery"
cd "$dir"

if [ ! -d BurdockWidgets.xcodeproj ]; then
  xcodegen generate
fi

xcodebuild \
  -project BurdockWidgets.xcodeproj \
  -scheme BurdockWidgetGallery \
  -configuration Release \
  -sdk iphonesimulator \
  -destination "generic/platform=iOS Simulator" \
  SYMROOT="$dir/build" \
  OBJROOT="$dir/build/obj" \
  CODE_SIGNING_ALLOWED=NO \
  -quiet \
  build

app="$dir/build/Release-iphonesimulator/BurdockWidgetGallery.app"
[ -d "$app" ] || { echo "Gallery app not found at $app"; exit 1; }

# Newest available iPhone simulator (the macos-26 image ships several; names change between images).
udid="$(xcrun simctl list devices available -j | python3 -I -c '
import json, sys
devices = json.load(sys.stdin)["devices"]
found = [d for runtime, items in devices.items() if "iOS" in runtime for d in items if d["name"].startswith("iPhone") and d.get("isAvailable", True)]
print(found[-1]["udid"] if found else "")
')"
[ -n "$udid" ] || { echo "No iPhone simulator available"; xcrun simctl list devices available; exit 1; }
echo "Simulator $udid"

xcrun simctl boot "$udid" 2>/dev/null || true
xcrun simctl bootstatus "$udid" -b
xcrun simctl uninstall "$udid" "$bundle_id" 2>/dev/null || true
xcrun simctl install "$udid" "$app"
xcrun simctl launch "$udid" "$bundle_id"

container="$(xcrun simctl get_app_container "$udid" "$bundle_id" data)"
boards="$container/Documents/boards"
for _ in $(seq 1 60); do
  if [ -f "$boards/done.txt" ]; then break; fi
  sleep 2
done

mkdir -p "$out"
if [ ! -f "$boards/done.txt" ]; then
  echo "The gallery did not finish rendering within 120 s"
  xcrun simctl io "$udid" screenshot "$out/gallery-timeout.png" || true
  xcrun simctl spawn "$udid" log show --last 3m --predicate 'process == "BurdockWidgetGallery"' --style compact | tail -50 || true
  exit 1
fi

cp "$boards"/*.png "$out/"
xcrun simctl io "$udid" screenshot "$out/gallery-screen.png" || true
ls -la "$out"
