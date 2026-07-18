#!/usr/bin/env bash
# PostToolUse hook: enforce the asset-preview gate.
# Fires after any asset-generating action (Meshy MCP tools, or the asset-bot CLI
# via Bash) and reminds the model that the asset is NOT done until it is wired
# into references/_view.html AND the localhost:8788 viewer is serving it.
input=$(cat)
tn=$(printf '%s' "$input" | jq -r '.tool_name // ""')
# Flatten newlines so multi-line Bash commands (asset-bot invocations often span
# several lines) still match the single-line, line-based grep below.
cmd=$(printf '%s' "$input" | jq -r '.tool_input.command // ""' | tr '\n' ' ')

is_asset=0
printf '%s' "$tn"  | grep -qE 'meshy_(download_model|image_to_3d|multi_image_to_3d|text_to_3d)' && is_asset=1
printf '%s' "$cmd" | grep -qiE 'asset-bot.*(generate|bootstrap)|index\.cjs.*(generate|bootstrap)' && is_asset=1

[ "$is_asset" = 0 ] && exit 0

jq -n '{
  systemMessage: "⚠️ PREVIEW GATE: asset is NOT done until it is in references/_view.html AND the :8788 viewer is running.",
  hookSpecificOutput: {
    hookEventName: "PostToolUse",
    additionalContext: "ASSET PREVIEW GATE (user standing rule): you just produced an asset. It is NOT complete until (1) it has a <model-viewer> (GLB) or <img> (2D, on a scene-tone bg) tag in references/_view.html, and (2) the viewer is served — cd references && python3 -m http.server 8788 — reachable at http://localhost:8788/_view.html. Wire it in and confirm HTTP 200 BEFORE reporting the asset done. An inline chat image is NOT a substitute."
  }
}'
