#!/bin/sh
set -eu
test_root=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$test_root/../.." && pwd)
build_dir=$(mktemp -d)
trap 'rm -rf "$build_dir"' EXIT HUP INT TERM
cc -std=c11 -Wall -Wextra -Werror -fsanitize=address,undefined -g \
  -I"$test_root/include" -I"$repo_root/firmware/esp32-gateway/main" \
  "$test_root/test_protocols.c" \
  "$repo_root/firmware/esp32-gateway/main/obd_protocol.c" \
  "$repo_root/firmware/esp32-gateway/main/gateway_protocol.c" \
  -o "$build_dir/test_protocols"
ASAN_OPTIONS=detect_leaks=0 "$build_dir/test_protocols"
