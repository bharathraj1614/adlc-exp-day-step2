#!/bin/sh
set -e

HTML_DIR="/usr/share/nginx/html"
INDEX_FILE="$HTML_DIR/index.html"

if [ -f "$INDEX_FILE" ]; then
  API_URL="${VITE_API_URL:-}"
  ESCAPED_API_URL=$(printf '%s' "$API_URL" | sed -e 's/[\/&]/\\&/g')
  sed -i "s|__VITE_API_URL__|$ESCAPED_API_URL|g" "$INDEX_FILE"
fi

exec nginx -g "daemon off;"
